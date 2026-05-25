using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThunderEagle.FilterForge.Core.Models;
using ThunderEagle.FilterForge.Core.Serialization;
using ThunderEagle.FilterForge.Core.Validation;
using System.Text.Json;

namespace ThunderEagle.FilterForge.App.ViewModels;

public partial class RawEditorViewModel : ObservableObject
{
    private readonly IFilterValidator _validator;
    private readonly Action<FilterRuleset> _onApply;

    [ObservableProperty]
    private string _jsonText;

    [ObservableProperty]
    private string _statusMessage = "Edit JSON directly, then click Validate to check or Apply to update the visual editor.";

    [ObservableProperty]
    private bool _hasError;

    /// <summary>Findings from the last Validate or Apply attempt. Empty when no issues.</summary>
    public ObservableCollection<ValidationIssue> Issues { get; } = [];

    public RawEditorViewModel(IFilterValidator validator, string initialJson, Action<FilterRuleset> onApply)
    {
        _validator = validator;
        _jsonText  = initialJson;
        _onApply   = onApply;
    }

    [RelayCommand]
    private void Validate()
    {
        if (TryParseAndValidate(out _, out var summary))
            SetStatus(summary, error: false);
        else
            SetStatus(summary, error: true);
    }

    [RelayCommand]
    private void Apply()
    {
        if (!TryParseAndValidate(out var ruleset, out var summary))
        {
            SetStatus(summary, error: true);
            return;
        }
        _onApply(ruleset!);
        SetStatus("Applied to visual editor.", error: false);
    }

    private bool TryParseAndValidate(out FilterRuleset? ruleset, out string summary)
    {
        Issues.Clear();
        ruleset = null;

        if (string.IsNullOrWhiteSpace(JsonText))
        {
            summary = "Nothing to validate.";
            return false;
        }

        try
        {
            ruleset = JsonSerializer.Deserialize<FilterRuleset>(JsonText, FilterJsonOptions.Default)
                      ?? throw new InvalidOperationException("Deserialised to null.");
        }
        catch (Exception ex)
        {
            Issues.Add(new ValidationIssue(ValidationSeverity.Error, $"Parse error: {ex.Message}"));
            summary = $"Parse error: {ex.Message}";
            return false;
        }

        var result = _validator.Validate(ruleset);
        foreach (var issue in result.Issues)
            Issues.Add(issue);

        if (!result.IsValid)
        {
            var errorCount = result.Errors.Count();
            summary = errorCount == 1
                ? "1 validation error — see panel below."
                : $"{errorCount} validation errors — see panel below.";
            return false;
        }

        var ruleCount = ruleset.Rules.Count;
        summary = $"Valid — {ruleCount} rule{(ruleCount == 1 ? "" : "s")}.";
        return true;
    }

    private void SetStatus(string message, bool error)
    {
        StatusMessage = message;
        HasError      = error;
    }
}

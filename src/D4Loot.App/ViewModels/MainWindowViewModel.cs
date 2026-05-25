using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D4Loot.App.ViewModels.Conditions;
using D4Loot.Core.Codec;
using D4Loot.Core.Models;
using D4Loot.Core.Serialization;
using D4Loot.Core.Validation;
using Microsoft.Win32;

namespace D4Loot.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IConditionViewModelFactory _conditionFactory;
    private readonly IFilterValidator _validator;

    public MainWindowViewModel(IConditionViewModelFactory conditionFactory, IFilterValidator validator)
    {
        _conditionFactory = conditionFactory;
        _validator        = validator;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCodeCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveJsonCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenRawEditorCommand))]
    private VisualEditorViewModel? _editor;

    [ObservableProperty]
    private string _statusMessage = "Import a filter code or open a JSON file to get started.";

    [ObservableProperty]
    private bool _hasError;

    /// <summary>Raised when the Raw Editor window should be opened. Handler must show the window.</summary>
    public event Action<RawEditorViewModel>? ShowRawEditorRequested;

    // ── Filter lifecycle ──────────────────────────────────────────────────

    [RelayCommand]
    private void NewFilter()
    {
        Editor = new VisualEditorViewModel(_conditionFactory,new FilterRuleset("New Filter", []));
        SetStatus("New filter created.", error: false);
    }

    // ── Import ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void PasteCode()
    {
        var text = Clipboard.GetText()?.Trim();
        if (string.IsNullOrEmpty(text)) { SetStatus("Clipboard is empty.", error: true); return; }
        TryLoadCode(text);
    }

    [RelayCommand]
    private void OpenJson()
    {
        var dlg = new OpenFileDialog
        {
            Title      = "Open Filter JSON",
            Filter     = "Filter JSON|*.json|All Files|*.*",
            DefaultExt = ".json",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var json    = File.ReadAllText(dlg.FileName);
            var ruleset = JsonSerializer.Deserialize<FilterRuleset>(json, FilterJsonOptions.Default)
                          ?? throw new InvalidOperationException("File deserialised to null.");
            Editor = new VisualEditorViewModel(_conditionFactory,ruleset);
            SetStatus($"Opened \"{Path.GetFileName(dlg.FileName)}\".", error: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Open failed: {ex.Message}", error: true);
        }
    }

    // ── Export ────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasEditor))]
    private void CopyCode()
    {
        try
        {
            var ruleset = Editor!.BuildRuleset();
            var result = _validator.Validate(ruleset);
            if (!result.IsValid)
            {
                SetStatus(string.Join(" ", result.Errors.Select(e => e.Message)), error: true);
                return;
            }
            Clipboard.SetText(FilterCodec.Encode(ruleset));
            SetStatus("Filter code copied to clipboard.", error: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}", error: true);
        }
    }

    [RelayCommand(CanExecute = nameof(HasEditor))]
    private void SaveJson()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Save Filter JSON",
            Filter     = "Filter JSON|*.json|All Files|*.*",
            DefaultExt = ".json",
            FileName   = Editor!.FilterName,
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var ruleset = Editor.BuildRuleset();
            var result = _validator.Validate(ruleset);
            if (!result.IsValid)
            {
                SetStatus(string.Join(" ", result.Errors.Select(e => e.Message)), error: true);
                return;
            }
            var json = JsonSerializer.Serialize(ruleset, FilterJsonOptions.Default);
            File.WriteAllText(dlg.FileName, json);
            SetStatus($"Saved \"{Path.GetFileName(dlg.FileName)}\".", error: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}", error: true);
        }
    }

    // ── Raw Editor ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasEditor))]
    private void OpenRawEditor()
    {
        var ruleset = Editor!.BuildRuleset();
        var errors = ruleset.Validate();
        if (errors.Count > 0)
        {
            SetStatus(string.Join(" ", errors), error: true);
            return;
        }
        var json = JsonSerializer.Serialize(ruleset, FilterJsonOptions.Default);
        ShowRawEditorRequested?.Invoke(new RawEditorViewModel(_validator, json, ApplyFromRawEditor));
    }

    private void ApplyFromRawEditor(FilterRuleset ruleset)
    {
        Editor = new VisualEditorViewModel(_conditionFactory,ruleset);
        SetStatus("Filter updated from Raw Editor.", error: false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void TryLoadCode(string code)
    {
        try
        {
            var ruleset = FilterCodec.Decode(code);
            Editor = new VisualEditorViewModel(_conditionFactory,ruleset);
            SetStatus($"Loaded \"{ruleset.Name}\" — {ruleset.Rules.Count} rule(s).", error: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Invalid filter code: {ex.Message}", error: true);
        }
    }

    private void SetStatus(string message, bool error)
    {
        StatusMessage = message;
        HasError      = error;
    }

    private bool HasEditor() => Editor is not null;
}

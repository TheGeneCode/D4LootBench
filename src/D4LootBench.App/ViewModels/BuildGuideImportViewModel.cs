using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D4LootBench.Ai.Import;
using D4LootBench.Core.Import;
using D4LootBench.Core.Models;

namespace D4LootBench.App.ViewModels;

public sealed record FormatOption(BuildGuideFormat Format, string Label);

public partial class BuildGuideImportViewModel(
    BuildGuideImporter importer,
    BuildGuideFilterGenerator generator) : ObservableObject
{
    public static IReadOnlyList<FormatOption> FormatOptions { get; } =
    [
        new(BuildGuideFormat.Auto,       "Auto-detect"),
        new(BuildGuideFormat.Mobalytics, "Mobalytics"),
        new(BuildGuideFormat.Maxroll,    "Maxroll"),
        new(BuildGuideFormat.IcyVeins,   "Icy Veins"),
    ];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private string _pastedText = "";

    [ObservableProperty]
    private FormatOption _selectedFormatOption = FormatOptions[0];

    [ObservableProperty]
    private IReadOnlyList<string> _warnings = [];

    [ObservableProperty]
    private bool _hasWarnings;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _hasStatus;

    /// <summary>Set after a successful import; read by the dialog owner to apply the result.</summary>
    public FilterRuleset? ImportedRuleset { get; private set; }

    /// <summary>Raised when the import succeeds. The dialog code-behind uses this to set DialogResult=true.</summary>
    public event Action? ImportSucceeded;

    [RelayCommand(CanExecute = nameof(CanImport))]
    private void Import()
    {
        HasError    = false;
        Warnings    = [];
        HasWarnings = false;

        try
        {
            var guide  = importer.Import(PastedText.Trim(), SelectedFormatOption.Format);
            var result = generator.Generate(guide);

            ImportedRuleset = result.Ruleset;
            Warnings        = result.Warnings;
            HasWarnings     = result.Warnings.Count > 0;
            HasStatus       = HasWarnings;

            if (HasWarnings)
                StatusText = $"{result.Warnings.Count} affix name(s) could not be resolved — see warnings below.";
            else
                StatusText = "";

            ImportSucceeded?.Invoke();
        }
        catch (BuildGuideImportException ex)
        {
            StatusText = ex.Message;
            HasError   = true;
            HasStatus  = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Import failed: {ex.Message}";
            HasError   = true;
            HasStatus  = true;
        }
    }

    private bool CanImport() => !string.IsNullOrWhiteSpace(PastedText);
}

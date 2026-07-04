using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D4LootBench.Core.Codec;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
using D4LootBench.Core.Models;
using D4LootBench.Core.Progression;

namespace D4LootBench.App.ViewModels.Progression;

/// <summary>Orchestrates the progression wizard: read gear screenshots, review the parsed drafts, paste a
/// goal build guide, then diff → generate → encode a native progression filter. Step lifecycle is driven
/// by <see cref="CurrentStep"/>; Core logic is injected for headless testability.</summary>
public sealed partial class ProgressionWizardViewModel : ObservableObject
{
    private readonly IGearReader _reader;
    private readonly GearTooltipParser _parser;
    private readonly BuildGuideImporter _importer;
    private readonly GoalBuildFactory _goalFactory;
    private readonly SlotDiffEngine _diffEngine;
    private readonly ProgressionFilterGenerator _generator;
    private readonly WeaponRoleMap _roleMap;
    private readonly Action<string> _setClipboard;

    private readonly List<GearParseResult> _parsed = [];
    private GearReviewSession? _session;

    [ObservableProperty]
    private ProgressionStep _currentStep = ProgressionStep.ReadGear;

    [ObservableProperty]
    private string _pastedText = "";

    [ObservableProperty]
    private PlayerClass _selectedClass = PlayerClass.All;

    [ObservableProperty]
    private FormatOption _selectedFormatOption = FormatOptions[0];

    [ObservableProperty]
    private string _shareCode = "";

    [ObservableProperty]
    private IReadOnlyList<string> _warnings = [];

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _hasError;

    /// <summary>Initializes a new instance of the <see cref="ProgressionWizardViewModel"/> class.</summary>
    /// <param name="reader">The gear reader seam.</param>
    /// <param name="parser">The tooltip parser.</param>
    /// <param name="importer">The build-guide importer.</param>
    /// <param name="goalFactory">The goal-build factory.</param>
    /// <param name="diffEngine">The slot-diff engine.</param>
    /// <param name="generator">The progression filter generator.</param>
    /// <param name="roleMap">The weapon slot-role map, used to classify equipped weapons per class.</param>
    /// <param name="setClipboard">Clipboard write; defaults to the WPF clipboard, overridable for tests.</param>
    public ProgressionWizardViewModel(
        IGearReader reader,
        GearTooltipParser parser,
        BuildGuideImporter importer,
        GoalBuildFactory goalFactory,
        SlotDiffEngine diffEngine,
        ProgressionFilterGenerator generator,
        WeaponRoleMap roleMap,
        Action<string>? setClipboard = null)
    {
        _reader = reader;
        _parser = parser;
        _importer = importer;
        _goalFactory = goalFactory;
        _diffEngine = diffEngine;
        _generator = generator;
        _roleMap = roleMap;
        _setClipboard = setClipboard ?? System.Windows.Clipboard.SetText;
    }

    /// <summary>Raised when the owner should load the generated filter into the main editor.</summary>
    public event Action<FilterRuleset>? OpenInEditorRequested;

    /// <summary>Gets the build-guide format options (reused from the build-guide import VM).</summary>
    public static IReadOnlyList<FormatOption> FormatOptions => BuildGuideImportViewModel.FormatOptions;

    /// <summary>Gets the selectable character classes for the class picker.</summary>
    public static IReadOnlyList<PlayerClass> Classes { get; } = Enum.GetValues<PlayerClass>();

    /// <summary>Gets the review drafts, one per read item.</summary>
    public ObservableCollection<GearItemDraftViewModel> Items { get; } = [];

    /// <summary>Gets the generated ruleset after a successful generate; read for an optional "open in editor".</summary>
    public FilterRuleset? GeneratedRuleset { get; private set; }

    /// <summary>Reads one gear tooltip image, parses it, and appends a review draft. The view supplies the
    /// PNG stream (clipboard image or file). Safe to call repeatedly, one item per screenshot.</summary>
    /// <param name="image">The encoded tooltip image stream.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the item has been read (or the read has failed gracefully).</returns>
    public async Task AddGearFromImageAsync(Stream image, CancellationToken ct = default)
    {
        try
        {
            var lines = await _reader.ReadLinesAsync(image, ct);
            var result = _parser.Parse(lines);
            _parsed.Add(result);
            _session = null; // invalidate — rebuilt on entering Review
            Items.Add(new GearItemDraftViewModel(new GearReviewSession([result]).Items[0]));
            SetStatus($"Read {result.Item.Slot} ({Items.Count} item(s)).", error: false);
            NextToReviewCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            SetStatus($"Read failed: {ex.Message}", error: true);
        }
    }

    [RelayCommand]
    private void RemoveItem(GearItemDraftViewModel item)
    {
        var idx = Items.IndexOf(item);
        if (idx < 0)
        {
            return;
        }

        Items.RemoveAt(idx);
        _parsed.RemoveAt(idx);
        _session = null;
        NextToReviewCommand.NotifyCanExecuteChanged();
        GenerateCommand.NotifyCanExecuteChanged();
    }

    // The step-1 Items drafts are throwaway previews (each wrapped in a private one-item session). On
    // entering Review, rebuild ONE authoritative session over all parsed items and repopulate Items from
    // it so _session.Build() reflects the user's edits.
    [RelayCommand(CanExecute = nameof(HasItems))]
    private void NextToReview()
    {
        _session = new GearReviewSession(_parsed);
        Items.Clear();
        foreach (var d in _session.Items)
        {
            Items.Add(new GearItemDraftViewModel(d));
        }

        CurrentStep = ProgressionStep.Review;
    }

    private bool HasItems() => Items.Count > 0;

    [RelayCommand]
    private void BackToRead() => CurrentStep = ProgressionStep.ReadGear;

    [RelayCommand]
    private void NextToGoal() => CurrentStep = ProgressionStep.Goal;

    [RelayCommand]
    private void BackToReview() => CurrentStep = ProgressionStep.Review;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private void Generate()
    {
        HasError = false;
        Warnings = [];
        StatusText = "";
        try
        {
            var guide = _importer.Import(PastedText.Trim(), SelectedFormatOption.Format);

            // A slot is "done" (no rule) only once the equipped item is maxed on its target affixes for its
            // rarity AND already holds the maximum catchable Greater Affixes — i.e. no upgrade a static filter
            // can detect remains. Every other slot gets a Recolor rule highlighting items that improve on
            // what's equipped (gold for a target-affix gain, cyan once the slot is maxed and only GAs remain).
            var goal = _goalFactory.Create(guide, MeetsGoalThreshold.RelativeToEquipped, SelectedClass, "Progression Filter");
            var loadout = EquippedLoadout.FromItems(_session!.Build(), out var loadoutWarnings, SelectedClass, _roleMap);
            var diff = _diffEngine.Diff(loadout, goal.GoalBuild);
            var filter = _generator.Generate(diff, SelectedClass, "Progression Filter");

            GeneratedRuleset = filter.Ruleset;
            ShareCode = FilterCodec.Encode(filter.Ruleset);
            Warnings = [.. loadoutWarnings, .. goal.Warnings, .. filter.Warnings];
            SetStatus(
                $"Generated {filter.TotalRuleCount} rule(s) for {diff.SlotsNeedingRules.Count} slot(s) needing upgrades.",
                error: false);
            CurrentStep = ProgressionStep.Result;
        }
        catch (BuildGuideImportException ex)
        {
            SetStatus(ex.Message, error: true);
        }
        catch (Exception ex)
        {
            SetStatus($"Generation failed: {ex.Message}", error: true);
        }
    }

    private bool CanGenerate() => _session is not null && !string.IsNullOrWhiteSpace(PastedText);

    [RelayCommand(CanExecute = nameof(HasShareCode))]
    private void CopyCode()
    {
        _setClipboard(ShareCode);
        SetStatus("Share code copied to clipboard.", error: false);
    }

    private bool HasShareCode() => !string.IsNullOrEmpty(ShareCode);

    [RelayCommand(CanExecute = nameof(HasShareCode))]
    private void OpenInEditor()
    {
        if (GeneratedRuleset is not null)
        {
            OpenInEditorRequested?.Invoke(GeneratedRuleset);
        }
    }

    [RelayCommand]
    private void StartOver()
    {
        _parsed.Clear();
        _session = null;
        Items.Clear();
        PastedText = "";
        ShareCode = "";
        Warnings = [];
        GeneratedRuleset = null;
        CurrentStep = ProgressionStep.ReadGear;
        NextToReviewCommand.NotifyCanExecuteChanged();
    }

    partial void OnPastedTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();

    partial void OnShareCodeChanged(string value)
    {
        CopyCodeCommand.NotifyCanExecuteChanged();
        OpenInEditorCommand.NotifyCanExecuteChanged();
    }

    private void SetStatus(string message, bool error)
    {
        StatusText = message;
        HasError = error;
    }
}

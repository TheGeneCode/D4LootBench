using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D4LootBench.Core.Codec;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
using D4LootBench.Core.Models;
using D4LootBench.Core.Profiles;
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
    private readonly ProgressionFilterMerger _merger;
    private readonly WeaponRoleMap _roleMap;
    private readonly ProfileStore _profileStore;
    private readonly Action<string> _setClipboard;
    private readonly Func<string> _getClipboard;
    private readonly Func<FilterRuleset, string, string?> _editBlock;
    private readonly Func<string, bool> _confirm;

    private readonly List<GearParseResult> _parsed = [];
    private GearReviewSession? _session;
    private ProgressionProfile? _activeProfile;

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

    [ObservableProperty]
    private ProgressionProfile? _selectedProfile;

    [ObservableProperty]
    private string _activeProfileName = ""; // "" = unsaved session; bound in the window header

    [ObservableProperty]
    private string _renameInput = ""; // Profiles step: new name for the selected profile

    [ObservableProperty]
    private string _saveAsName = ""; // Result step: name for "Save as profile"

    [ObservableProperty]
    private string _overrideBlockCode = "";

    [ObservableProperty]
    private string _overriddenByBlockCode = "";

    /// <summary>Initializes a new instance of the <see cref="ProgressionWizardViewModel"/> class.</summary>
    /// <param name="reader">The gear reader seam.</param>
    /// <param name="parser">The tooltip parser.</param>
    /// <param name="importer">The build-guide importer.</param>
    /// <param name="goalFactory">The goal-build factory.</param>
    /// <param name="diffEngine">The slot-diff engine.</param>
    /// <param name="generator">The progression filter generator.</param>
    /// <param name="merger">The static-block merge engine that wraps better-gear with the override/overridden-by blocks.</param>
    /// <param name="roleMap">The weapon slot-role map, used to classify equipped weapons per class.</param>
    /// <param name="profileStore">The file-backed profile store for saved progression sessions.</param>
    /// <param name="setClipboard">Clipboard write; defaults to the WPF clipboard, overridable for tests.</param>
    /// <param name="getClipboard">Clipboard read; defaults to the WPF clipboard, overridable for tests.</param>
    /// <param name="editBlock">Static-block editor seam: given a block's ruleset and a dialog title, opens the
    /// modal rule editor and returns the edited block's share code, or <c>null</c> on Cancel. Defaults to a
    /// no-op returning <c>null</c> so headless construction (and tests) work without a WPF window.</param>
    /// <param name="confirm">Destructive-action confirmation seam; defaults to a WPF Yes/No message box.</param>
    public ProgressionWizardViewModel(
        IGearReader reader,
        GearTooltipParser parser,
        BuildGuideImporter importer,
        GoalBuildFactory goalFactory,
        SlotDiffEngine diffEngine,
        ProgressionFilterGenerator generator,
        ProgressionFilterMerger merger,
        WeaponRoleMap roleMap,
        ProfileStore profileStore,
        Action<string>? setClipboard = null,
        Func<string>? getClipboard = null,
        Func<FilterRuleset, string, string?>? editBlock = null,
        Func<string, bool>? confirm = null)
    {
        _reader = reader;
        _parser = parser;
        _importer = importer;
        _goalFactory = goalFactory;
        _diffEngine = diffEngine;
        _generator = generator;
        _merger = merger;
        _roleMap = roleMap;
        _profileStore = profileStore;
        _setClipboard = setClipboard ?? System.Windows.Clipboard.SetText;
        _getClipboard = getClipboard ?? System.Windows.Clipboard.GetText;
        _editBlock = editBlock ?? ((_, _) => null);
        _confirm = confirm ?? (msg => System.Windows.MessageBox.Show(
            msg,
            "D4LootBench",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes);

        // Land on the Profiles home when any profile exists; first-run UX (empty store) is unchanged.
        // Assign the backing field directly — no change notification is needed during construction.
        RefreshProfiles();
        _currentStep = Profiles.Count > 0 ? ProgressionStep.Profiles : ProgressionStep.ReadGear;
    }

    /// <summary>Raised when the owner should load the generated filter into the main editor.</summary>
    public event Action<FilterRuleset>? OpenInEditorRequested;

    /// <summary>Gets the build-guide format options (reused from the build-guide import VM).</summary>
    public static IReadOnlyList<FormatOption> FormatOptions => BuildGuideImportViewModel.FormatOptions;

    /// <summary>Gets the selectable character classes for the class picker.</summary>
    public static IReadOnlyList<PlayerClass> Classes { get; } = Enum.GetValues<PlayerClass>();

    /// <summary>Gets the review drafts, one per read item.</summary>
    public ObservableCollection<GearItemDraftViewModel> Items { get; } = [];

    /// <summary>Gets the saved profiles (newest-modified first) shown on the Profiles landing step.</summary>
    public ObservableCollection<ProgressionProfile> Profiles { get; } = [];

    /// <summary>Gets the generated ruleset after a successful generate; read for an optional "open in editor".</summary>
    public FilterRuleset? GeneratedRuleset { get; private set; }

    /// <summary>Gets the rule count of the override block (0 when empty or undecodable), for the UI.</summary>
    public int OverrideRuleCount => CountRules(OverrideBlockCode);

    /// <summary>Gets the rule count of the overridden-by block (0 when empty or undecodable), for the UI.</summary>
    public int OverriddenByRuleCount => CountRules(OverriddenByBlockCode);

    private static int CountRules(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return 0;
        }

        try
        {
            return FilterCodec.Decode(code).Rules.Count;
        }
        catch
        {
            return 0;
        }
    }

    private static IReadOnlyList<FilterRule> DecodeBlock(string code)
        => string.IsNullOrWhiteSpace(code) ? [] : FilterCodec.Decode(code).Rules;

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

    [RelayCommand(CanExecute = nameof(HasItems))]
    private void NextToReview()
    {
        RebuildReviewSession();
        CurrentStep = ProgressionStep.Review;
    }

    // The step-1 Items drafts are throwaway previews (each wrapped in a private one-item session). Rebuild
    // ONE authoritative session over all parsed items and repopulate Items from it so _session.Build()
    // reflects the user's edits. Shared by NextToReview and OpenSelectedProfile (which restores gear).
    private void RebuildReviewSession()
    {
        _session = new GearReviewSession(_parsed);
        Items.Clear();
        foreach (var d in _session.Items)
        {
            Items.Add(new GearItemDraftViewModel(d));
        }
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

            // Wrap the better-gear ruleset with the optional static blocks; the exported ShareCode is the
            // merged code so regenerating never loses the user's override / overridden-by rules.
            var merged = _merger.Merge(
                DecodeBlock(OverrideBlockCode),
                filter.Ruleset,
                DecodeBlock(OverriddenByBlockCode),
                "Progression Filter");

            GeneratedRuleset = merged.Ruleset;
            ShareCode = FilterCodec.Encode(merged.Ruleset);

            var validationErrors = merged.Ruleset.Validate();
            Warnings = [.. loadoutWarnings, .. goal.Warnings, .. filter.Warnings, .. merged.Warnings, .. validationErrors];
            SetStatus(
                validationErrors.Count > 0
                    ? $"Generated with {validationErrors.Count} validation error(s) — review before importing."
                    : $"Generated {merged.Ruleset.Rules.Count} merged rule(s) for {diff.SlotsNeedingRules.Count} slot(s) needing upgrades.",
                error: validationErrors.Count > 0);
            CurrentStep = ProgressionStep.Result;

            // Auto-save only on a successful generate, and only when a profile is active — kept inside the
            // try tail so a failed generate never persists a half-baked session.
            AutoSaveActiveProfile();
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
    private void ImportOverrideBlock() => ImportBlockFromClipboard(isOverride: true);

    [RelayCommand]
    private void ImportOverriddenByBlock() => ImportBlockFromClipboard(isOverride: false);

    // Decodes-and-validates a share code from the clipboard into the target block. On any decode failure the
    // block is left untouched and an error status is surfaced, so a bad paste never clobbers a good block.
    private void ImportBlockFromClipboard(bool isOverride)
    {
        var code = _getClipboard().Trim();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Clipboard is empty — copy a filter share code first.", error: true);
            return;
        }

        int count;
        try
        {
            count = FilterCodec.Decode(code).Rules.Count;
        }
        catch (Exception ex)
        {
            SetStatus($"Not a valid filter share code: {ex.Message}", error: true);
            return;
        }

        var label = isOverride ? "override" : "overridden-by";
        if (isOverride)
        {
            OverrideBlockCode = code;
        }
        else
        {
            OverriddenByBlockCode = code;
        }

        SetStatus($"Imported {count} rule(s) into the {label} block.", error: false);
    }

    [RelayCommand]
    private void ClearOverrideBlock() => OverrideBlockCode = "";

    [RelayCommand]
    private void ClearOverriddenByBlock() => OverriddenByBlockCode = "";

    [RelayCommand]
    private void EditOverrideBlock() => EditBlock(isOverride: true);

    [RelayCommand]
    private void EditOverriddenByBlock() => EditBlock(isOverride: false);

    // Opens the modal rule editor (via the _editBlock seam) seeded from the block's current code — or an empty
    // named ruleset when the block is blank — and writes the edited code back on OK. Cancel leaves it untouched,
    // mirroring the "never clobber a good block on failure" rule that import follows.
    private void EditBlock(bool isOverride)
    {
        var code = isOverride ? OverrideBlockCode : OverriddenByBlockCode;
        var label = isOverride ? "override" : "overridden-by";
        FilterRuleset ruleset;
        if (string.IsNullOrWhiteSpace(code))
        {
            ruleset = new FilterRuleset(isOverride ? "Override Rules" : "Overridden-By Rules", []);
        }
        else
        {
            try
            {
                ruleset = FilterCodec.Decode(code);
            }
            catch (Exception ex)
            {
                // A block code can reach here without having passed through ImportBlockFromClipboard's
                // validation — e.g. restored verbatim from a persisted profile (OpenSelectedProfile) that
                // was hand-edited or corrupted on disk. Guard the same way Import/CountRules already do:
                // never let a bad stored code crash the app, and never clobber it since we can't decode it.
                SetStatus($"Cannot edit the {label} block: {ex.Message}", error: true);
                return;
            }
        }

        var title = isOverride ? "Edit Override Rules" : "Edit Overridden-By Rules";

        var result = _editBlock(ruleset, title);
        if (result is null)
        {
            return; // cancelled
        }

        if (isOverride)
        {
            OverrideBlockCode = result;
        }
        else
        {
            OverriddenByBlockCode = result;
        }

        SetStatus($"Updated the {label} block.", error: false);
    }

    [RelayCommand]
    private void NextToStaticRules() => CurrentStep = ProgressionStep.StaticRules;

    [RelayCommand]
    private void BackToGoal() => CurrentStep = ProgressionStep.Goal;

    // Clears the in-flight gear/guide/result session state without touching profile bookkeeping or
    // navigating. Callers decide where to land and whether to clear the active profile.
    private void ResetSession()
    {
        _parsed.Clear();
        _session = null;
        Items.Clear();
        PastedText = "";
        OverrideBlockCode = "";
        OverriddenByBlockCode = "";
        ShareCode = "";
        Warnings = [];
        GeneratedRuleset = null;
        NextToReviewCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void StartOver()
    {
        ResetSession();
        _activeProfile = null;
        ActiveProfileName = "";
        SaveAsName = "";
        RefreshProfiles();
        CurrentStep = ProgressionStep.Profiles; // the wizard's home
    }

    [RelayCommand]
    private void StartNewProfile()
    {
        ResetSession();
        _activeProfile = null;
        ActiveProfileName = "";
        SaveAsName = "";
        CurrentStep = ProgressionStep.ReadGear;
    }

    // Navigation only — keeps the current session so backing out to Profiles never loses in-flight work.
    [RelayCommand]
    private void GoToProfiles()
    {
        RefreshProfiles();
        CurrentStep = ProgressionStep.Profiles;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void OpenSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        _activeProfile = SelectedProfile;
        ActiveProfileName = SelectedProfile.Name;

        // Restore the verified loadout (no OCR) and the build/target.
        _parsed.Clear();
        _parsed.AddRange(SelectedProfile.Gear.Select(g => new GearParseResult
        {
            Item = g,
            Confidence = GearParseConfidence.High,
        }));
        PastedText = SelectedProfile.GuideText;
        OverrideBlockCode = SelectedProfile.OverrideBlockCode ?? "";
        OverriddenByBlockCode = SelectedProfile.OverriddenByBlockCode ?? "";
        SelectedClass = SelectedProfile.PlayerClass;
        SelectedFormatOption = FormatOptions.FirstOrDefault(o => o.Format == SelectedProfile.GuideFormat)
            ?? FormatOptions[0];

        // Discard any prior result state so the restored session starts clean.
        ShareCode = "";
        Warnings = [];
        GeneratedRuleset = null;
        HasError = false;

        if (_parsed.Count > 0)
        {
            RebuildReviewSession();
            CurrentStep = ProgressionStep.Review;
        }
        else
        {
            _session = null;
            Items.Clear();
            CurrentStep = ProgressionStep.ReadGear;
        }

        NextToReviewCommand.NotifyCanExecuteChanged();
        GenerateCommand.NotifyCanExecuteChanged();
        SetStatus($"Opened profile \"{SelectedProfile.Name}\".", error: false);
    }

    private bool HasSelectedProfile() => SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void DeleteSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var name = SelectedProfile.Name;
        if (!_confirm($"Delete profile \"{name}\"? This cannot be undone."))
        {
            return;
        }

        var deletedId = SelectedProfile.Id;
        _profileStore.Delete(deletedId);
        if (deletedId == _activeProfile?.Id)
        {
            _activeProfile = null;
            ActiveProfileName = "";
        }

        SelectedProfile = null;
        RefreshProfiles();
        SetStatus($"Deleted profile \"{name}\".", error: false);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void DuplicateSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            var copy = _profileStore.Duplicate(SelectedProfile.Id);
            RefreshProfiles();
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == copy.Id);
            SetStatus($"Duplicated profile as \"{copy.Name}\".", error: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRename))]
    private void RenameSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            var renamed = _profileStore.Rename(SelectedProfile.Id, RenameInput);
            if (_activeProfile is not null && renamed.Id == _activeProfile.Id)
            {
                _activeProfile = _activeProfile with { Name = renamed.Name };
                ActiveProfileName = renamed.Name;
            }

            RefreshProfiles();
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == renamed.Id);
            SetStatus($"Renamed profile to \"{renamed.Name}\".", error: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    private bool CanRename() => SelectedProfile is not null && !string.IsNullOrWhiteSpace(RenameInput);

    [RelayCommand(CanExecute = nameof(CanSaveAsProfile))]
    private void SaveAsProfile()
    {
        try
        {
            var saved = _profileStore.Save(SnapshotCurrentState(Guid.NewGuid(), SaveAsName.Trim(), default));
            _activeProfile = saved;
            ActiveProfileName = saved.Name;
            SaveAsName = "";
            RefreshProfiles();
            SetStatus($"Saved profile \"{saved.Name}\".", error: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    private bool CanSaveAsProfile() =>
        !string.IsNullOrWhiteSpace(SaveAsName) && (_session is not null || _parsed.Count > 0);

    // Reloads the profile list from disk; surfaces any skipped-file warnings rather than dropping them.
    private void RefreshProfiles()
    {
        var result = _profileStore.LoadAll();
        Profiles.Clear();
        foreach (var p in result.Profiles)
        {
            Profiles.Add(p);
        }

        if (result.Warnings.Count > 0)
        {
            SetStatus(string.Join(" ", result.Warnings), error: true);
        }
    }

    // Captures the live session (verified gear + build/target) into a profile record for persistence.
    private ProgressionProfile SnapshotCurrentState(Guid id, string name, DateTimeOffset createdUtc) => new()
    {
        Id = id,
        Name = name,
        CreatedUtc = createdUtc,
        PlayerClass = SelectedClass,
        GuideFormat = SelectedFormatOption.Format,
        GuideText = PastedText,
        Gear = _session?.Build() ?? [.. _parsed.Select(p => p.Item)],
        OverrideBlockCode = string.IsNullOrWhiteSpace(OverrideBlockCode) ? null : OverrideBlockCode,
        OverriddenByBlockCode = string.IsNullOrWhiteSpace(OverriddenByBlockCode) ? null : OverriddenByBlockCode,
    };

    private void AutoSaveActiveProfile()
    {
        if (_activeProfile is null)
        {
            return;
        }

        _activeProfile = _profileStore.Save(
            SnapshotCurrentState(_activeProfile.Id, _activeProfile.Name, _activeProfile.CreatedUtc));
        RefreshProfiles();
    }

    partial void OnSelectedProfileChanged(ProgressionProfile? value)
    {
        OpenSelectedProfileCommand.NotifyCanExecuteChanged();
        DeleteSelectedProfileCommand.NotifyCanExecuteChanged();
        DuplicateSelectedProfileCommand.NotifyCanExecuteChanged();
        RenameSelectedProfileCommand.NotifyCanExecuteChanged();
        RenameInput = value?.Name ?? "";
    }

    partial void OnRenameInputChanged(string value) => RenameSelectedProfileCommand.NotifyCanExecuteChanged();

    partial void OnSaveAsNameChanged(string value) => SaveAsProfileCommand.NotifyCanExecuteChanged();

    partial void OnPastedTextChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();

    partial void OnOverrideBlockCodeChanged(string value) => OnPropertyChanged(nameof(OverrideRuleCount));

    partial void OnOverriddenByBlockCodeChanged(string value) => OnPropertyChanged(nameof(OverriddenByRuleCount));

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

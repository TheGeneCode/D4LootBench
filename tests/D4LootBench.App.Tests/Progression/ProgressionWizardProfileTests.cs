using D4LootBench.App.ViewModels.Progression;
using D4LootBench.Core.Codec;
using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
using D4LootBench.Core.Models;
using D4LootBench.Core.Profiles;
using D4LootBench.Core.Progression;
using Shouldly;

namespace D4LootBench.App.Tests.Progression;

/// <summary>Covers the profile lifecycle wired into the wizard VM: landing step, save-as, open/restore,
/// auto-save on generate, and the delete/duplicate/rename commands.</summary>
public sealed class ProgressionWizardProfileTests : IDisposable
{
    // Self-contained fixtures (test classes stay independent of the sibling VM test file).
    private const string Guide =
        "Helm\nCritical Strike Chance\nMaximum Life\nCooldown Reduction\n" +
        "Gloves\nCritical Strike Chance\nMaximum Life\nCooldown Reduction\n";

    private static readonly IReadOnlyList<string> HelmLines =
    [
        "Ancestral Legendary Helm",
        "925 Item Power",
        "+45.0% Critical Strike Chance",
        "+112 Maximum Life",
        "+8.5% Cooldown Reduction",
        "Requires Level 60",
    ];

    private static readonly IReadOnlyList<string> GlovesLines =
    [
        "Ancestral Legendary Gloves",
        "925 Item Power",
        "+45.0% Critical Strike Chance",
        "+112 Maximum Life",
        "+8.5% Cooldown Reduction",
        "Requires Level 60",
    ];

    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "d4lb-profile-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void InitialStep_NoProfiles_ReadGear()
    {
        var vm = NewVm(NewStore(), new FakeGearReader(HelmLines));

        vm.CurrentStep.ShouldBe(ProgressionStep.ReadGear);
    }

    [Fact]
    public async Task InitialStep_WithProfiles_LandsOnProfiles()
    {
        var store = NewStore();
        await SavedProfileAsync(store);

        var vm = NewVm(store, new FakeGearReader(HelmLines));

        vm.CurrentStep.ShouldBe(ProgressionStep.Profiles);
        vm.Profiles.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SaveAsProfile_PersistsVerifiedGearAndTarget()
    {
        var store = NewStore();
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        await vm.AddGearFromImageAsync(Stream.Null);
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.SelectedClass = PlayerClass.Sorcerer;
        vm.SaveAsName = "P1";

        vm.SaveAsProfileCommand.Execute(null);

        var profiles = store.LoadAll().Profiles;
        profiles.Count.ShouldBe(1);
        var saved = profiles[0];
        saved.Gear.Count.ShouldBe(1);
        saved.Gear[0].Slot.ShouldBe(GearSlot.Helm);
        saved.GuideText.ShouldBe(Guide);
        saved.PlayerClass.ShouldBe(PlayerClass.Sorcerer);
        vm.ActiveProfileName.ShouldBe("P1");
    }

    [Fact]
    public async Task SaveAsProfile_BlankName_CannotExecute()
    {
        var store = NewStore();
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        await vm.AddGearFromImageAsync(Stream.Null);
        vm.NextToReviewCommand.Execute(null);

        vm.SaveAsName = " ";

        vm.SaveAsProfileCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task OpenSelectedProfile_RestoresGearAndTargetAndLandsOnReview()
    {
        var store = NewStore();
        var seeded = await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));

        vm.SelectedProfile = vm.Profiles[0];
        vm.OpenSelectedProfileCommand.Execute(null);

        vm.CurrentStep.ShouldBe(ProgressionStep.Review);
        vm.Items.Count.ShouldBe(1);
        vm.PastedText.ShouldBe(seeded.GuideText);
        vm.SelectedClass.ShouldBe(seeded.PlayerClass);
        vm.SelectedFormatOption.Format.ShouldBe(seeded.GuideFormat);
        vm.ActiveProfileName.ShouldBe(seeded.Name);
    }

    [Fact]
    public async Task Generate_WithActiveProfile_AutoSaves()
    {
        // Monotonic clock so the auto-save ModifiedUtc is provably later than the seed's.
        var store = NewStore(MonotonicClock());
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles[0];
        vm.OpenSelectedProfileCommand.Execute(null);
        var before = store.LoadAll().Profiles.Single();

        vm.GenerateCommand.Execute(null);

        vm.CurrentStep.ShouldBe(ProgressionStep.Result);
        var after = store.LoadAll().Profiles.Single();
        after.Id.ShouldBe(before.Id);
        after.ModifiedUtc.ShouldBeGreaterThan(before.ModifiedUtc);
        after.Gear.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Generate_WithoutActiveProfile_DoesNotSave()
    {
        var store = NewStore();
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        await vm.AddGearFromImageAsync(Stream.Null);
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;

        vm.GenerateCommand.Execute(null);

        vm.CurrentStep.ShouldBe(ProgressionStep.Result);
        store.LoadAll().Profiles.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReplaceGearPiece_ThenGenerate_UpdatesProfile()
    {
        var store = NewStore();
        var vm = NewVm(store, new SequenceGearReader(HelmLines, GlovesLines));
        await vm.AddGearFromImageAsync(Stream.Null); // helm
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.SaveAsName = "P1";
        vm.SaveAsProfileCommand.Execute(null); // profile now active

        vm.BackToReadCommand.Execute(null);
        vm.RemoveItemCommand.Execute(vm.Items[0]); // drop the helm
        await vm.AddGearFromImageAsync(Stream.Null); // gloves (second line set)
        vm.NextToReviewCommand.Execute(null);
        vm.GenerateCommand.Execute(null);

        var stored = store.LoadAll().Profiles.Single();
        stored.Gear.Count.ShouldBe(1);
        stored.Gear[0].Slot.ShouldBe(GearSlot.Gloves);
    }

    [Fact]
    public async Task DeleteSelectedProfile_ConfirmDeclined_Keeps()
    {
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines), confirm: _ => false);
        vm.SelectedProfile = vm.Profiles[0];

        vm.DeleteSelectedProfileCommand.Execute(null);

        store.LoadAll().Profiles.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteSelectedProfile_Confirmed_RemovesAndClearsActive()
    {
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines), confirm: _ => true);
        vm.SelectedProfile = vm.Profiles[0];
        vm.OpenSelectedProfileCommand.Execute(null); // make it the active profile
        vm.GoToProfilesCommand.Execute(null);
        vm.SelectedProfile = vm.Profiles[0];

        vm.DeleteSelectedProfileCommand.Execute(null);

        store.LoadAll().Profiles.ShouldBeEmpty();
        vm.ActiveProfileName.ShouldBe("");
        vm.Profiles.ShouldBeEmpty();
    }

    [Fact]
    public async Task DuplicateSelectedProfile_AddsCopyAndSelectsIt()
    {
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles[0];

        vm.DuplicateSelectedProfileCommand.Execute(null);

        vm.Profiles.Count.ShouldBe(2);
        vm.SelectedProfile!.Name.ShouldBe("P1 (copy)");
    }

    [Fact]
    public async Task RenameSelectedProfile_UpdatesListAndActiveName()
    {
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles[0];
        vm.OpenSelectedProfileCommand.Execute(null); // active profile
        vm.GoToProfilesCommand.Execute(null);
        vm.SelectedProfile = vm.Profiles[0];

        vm.RenameInput = "P2";
        vm.RenameSelectedProfileCommand.Execute(null);

        store.LoadAll().Profiles.Single().Name.ShouldBe("P2");
        vm.ActiveProfileName.ShouldBe("P2");
    }

    [Fact]
    public async Task StartOver_ReturnsToProfilesAndClearsActive()
    {
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles[0];
        vm.OpenSelectedProfileCommand.Execute(null);
        vm.GenerateCommand.Execute(null);

        vm.StartOverCommand.Execute(null);

        vm.CurrentStep.ShouldBe(ProgressionStep.Profiles);
        vm.ActiveProfileName.ShouldBe("");
        vm.Items.ShouldBeEmpty();
    }

    [Fact]
    public void OpenSelectedProfile_EmptyGearProfile_LandsOnReadGearAndRestoresGuideTextOnly()
    {
        // A profile can end up with no gear (e.g. every item removed before a prior save-as). Opening it
        // must not fabricate a session: CanGenerate stays false and the step falls back to ReadGear, while
        // the guide/class/format still restore so the user doesn't lose their target.
        var store = NewStore();
        var seeded = store.Save(new ProgressionProfile
        {
            Id = Guid.NewGuid(),
            Name = "EmptyGear",
            PlayerClass = PlayerClass.Necromancer,
            GuideFormat = BuildGuideFormat.Maxroll,
            GuideText = Guide,
            Gear = [],
        });
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles.Single(p => p.Id == seeded.Id);

        vm.OpenSelectedProfileCommand.Execute(null);

        vm.CurrentStep.ShouldBe(ProgressionStep.ReadGear);
        vm.Items.ShouldBeEmpty();
        vm.PastedText.ShouldBe(Guide);
        vm.SelectedClass.ShouldBe(PlayerClass.Necromancer);
        vm.SelectedFormatOption.Format.ShouldBe(BuildGuideFormat.Maxroll);
        vm.GenerateCommand.CanExecute(null).ShouldBeFalse();
        vm.ActiveProfileName.ShouldBe("EmptyGear");
    }

    [Fact]
    public async Task Generate_FailsWithActiveProfile_DoesNotAutoSaveOrChangeStoredProfile()
    {
        // Highest-risk auto-save path: a failed Generate on an *active* profile must not overwrite the
        // last-good save with the half-edited (unparsable) guide text that caused the failure.
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles[0];
        vm.OpenSelectedProfileCommand.Execute(null); // active, lands on Review (gear present)
        var before = store.LoadAll().Profiles.Single();
        vm.PastedText = "this text matches no known build-guide format at all";

        vm.GenerateCommand.Execute(null);

        vm.HasError.ShouldBeTrue();
        vm.CurrentStep.ShouldBe(ProgressionStep.Review); // failed generate never advances to Result
        var after = store.LoadAll().Profiles.Single();
        after.ModifiedUtc.ShouldBe(before.ModifiedUtc);
        after.GuideText.ShouldBe(before.GuideText); // not clobbered with the garbage text
    }

    [Fact]
    public async Task DeleteSelectedProfile_NonActiveProfile_LeavesActiveUntouched()
    {
        var store = NewStore();
        await SavedProfileAsync(store, "P1");
        await SavedProfileAsync(store, "P2");
        var vm = NewVm(store, new FakeGearReader(HelmLines), confirm: _ => true);
        vm.SelectedProfile = vm.Profiles.Single(p => p.Name == "P1");
        vm.OpenSelectedProfileCommand.Execute(null); // P1 becomes active
        vm.GoToProfilesCommand.Execute(null);
        vm.SelectedProfile = vm.Profiles.Single(p => p.Name == "P2");

        vm.DeleteSelectedProfileCommand.Execute(null);

        var remaining = store.LoadAll().Profiles;
        remaining.Count.ShouldBe(1);
        remaining[0].Name.ShouldBe("P1");
        vm.ActiveProfileName.ShouldBe("P1"); // deleting a bystander must not clear the active profile
    }

    [Fact]
    public async Task RenameSelectedProfile_NonActiveProfile_LeavesActiveNameUntouched()
    {
        var store = NewStore();
        await SavedProfileAsync(store, "P1");
        await SavedProfileAsync(store, "P2");
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles.Single(p => p.Name == "P1");
        vm.OpenSelectedProfileCommand.Execute(null); // P1 active
        vm.GoToProfilesCommand.Execute(null);
        vm.SelectedProfile = vm.Profiles.Single(p => p.Name == "P2");

        vm.RenameInput = "P2-renamed";
        vm.RenameSelectedProfileCommand.Execute(null);

        store.LoadAll().Profiles.ShouldContain(p => p.Name == "P2-renamed");
        vm.ActiveProfileName.ShouldBe("P1"); // renaming a bystander must not touch the active profile's name
    }

    [Fact]
    public async Task RenameSelectedProfile_SourceMissing_ReportsErrorWithoutThrowing()
    {
        // TOCTOU: the file backing the selection disappears (e.g. deleted by another process/window)
        // between selection and command execution. The store throws InvalidOperationException; the
        // command must catch it into an error status rather than crash the VM.
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles[0];
        vm.RenameInput = "P2";
        store.Delete(vm.SelectedProfile!.Id);

        vm.RenameSelectedProfileCommand.Execute(null);

        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldContain("not found");
    }

    [Fact]
    public async Task DuplicateSelectedProfile_SourceMissing_ReportsErrorWithoutThrowing()
    {
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles[0];
        store.Delete(vm.SelectedProfile!.Id);

        vm.DuplicateSelectedProfileCommand.Execute(null);

        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldContain("not found");
    }

    [Fact]
    public async Task ProfileCommands_NoSelection_CannotExecute()
    {
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));

        vm.SelectedProfile.ShouldBeNull();
        vm.OpenSelectedProfileCommand.CanExecute(null).ShouldBeFalse();
        vm.DeleteSelectedProfileCommand.CanExecute(null).ShouldBeFalse();
        vm.DuplicateSelectedProfileCommand.CanExecute(null).ShouldBeFalse();
        vm.RenameSelectedProfileCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task SelectedProfileChanged_ReseedsRenameInput_AcrossSelectionAndClear()
    {
        var store = NewStore();
        await SavedProfileAsync(store, "P1");
        await SavedProfileAsync(store, "P2");
        var vm = NewVm(store, new FakeGearReader(HelmLines));

        vm.SelectedProfile = vm.Profiles.Single(p => p.Name == "P1");
        vm.RenameInput.ShouldBe("P1");

        vm.SelectedProfile = vm.Profiles.Single(p => p.Name == "P2");
        vm.RenameInput.ShouldBe("P2"); // re-seeded from the new selection, not left stale from P1

        vm.SelectedProfile = null;
        vm.RenameInput.ShouldBe("");
    }

    [Fact]
    public async Task OpenSelectedProfile_RoundTripsGearFidelity_RarityPowerAncestralAndAffixes()
    {
        // Beyond Slot (already covered elsewhere): rarity, GA/ancestral flags, item power and affix count
        // must survive the real Save -> LoadAll -> OpenSelectedProfile path unchanged.
        var store = NewStore();
        var seeded = await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles.Single(p => p.Id == seeded.Id);

        vm.OpenSelectedProfileCommand.Execute(null);

        vm.Items.Count.ShouldBe(1);
        var expected = seeded.Gear[0];
        var item = vm.Items[0];
        item.Rarity.ShouldBe(expected.Rarity);
        item.IsAncestral.ShouldBe(expected.IsAncestral);
        item.ItemPower.ShouldBe(expected.ItemPower);
        item.Affixes.Count.ShouldBe(expected.Affixes.Count);
    }

    [Fact]
    public async Task StartNewProfile_ClearsActiveProfileAndSession_LandsOnReadGear()
    {
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles[0];
        vm.OpenSelectedProfileCommand.Execute(null); // active profile with restored gear/guide

        vm.StartNewProfileCommand.Execute(null);

        vm.CurrentStep.ShouldBe(ProgressionStep.ReadGear);
        vm.ActiveProfileName.ShouldBe("");
        vm.Items.ShouldBeEmpty();
        vm.PastedText.ShouldBeEmpty();
        vm.GenerateCommand.CanExecute(null).ShouldBeFalse();
        store.LoadAll().Profiles.Single().Name.ShouldBe("P1"); // the stored profile itself is untouched
    }

    [Fact]
    public async Task SaveAsProfile_NameWithSurroundingWhitespace_IsTrimmed()
    {
        var store = NewStore();
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        await vm.AddGearFromImageAsync(Stream.Null);
        vm.NextToReviewCommand.Execute(null);
        vm.SaveAsName = "  P1  ";

        vm.SaveAsProfileCommand.Execute(null);

        store.LoadAll().Profiles.Single().Name.ShouldBe("P1");
        vm.ActiveProfileName.ShouldBe("P1");
    }

    [Fact]
    public async Task SaveAndOpen_roundtrips_block_codes()
    {
        var store = NewStore();
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        await vm.AddGearFromImageAsync(Stream.Null);
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        var overrideCode = CodeWith(new FilterRule("Show Mythics", Visibility.Show, 0, []));
        var overriddenByCode = CodeWith(new FilterRule("Hide Rares Later", Visibility.Recolor, 0, []));
        vm.OverrideBlockCode = overrideCode;
        vm.OverriddenByBlockCode = overriddenByCode;
        vm.SaveAsName = "P1";
        vm.SaveAsProfileCommand.Execute(null);

        // A fresh VM sharing the same store restores both codes from disk.
        var fresh = NewVm(store, new FakeGearReader(HelmLines));
        fresh.SelectedProfile = fresh.Profiles.Single(p => p.Name == "P1");
        fresh.OpenSelectedProfileCommand.Execute(null);

        fresh.OverrideBlockCode.ShouldBe(overrideCode);
        fresh.OverriddenByBlockCode.ShouldBe(overriddenByCode);
    }

    [Fact]
    public async Task Autosave_after_generate_persists_block_codes()
    {
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles[0];
        vm.OpenSelectedProfileCommand.Execute(null); // active profile with gear + guide
        var overrideCode = CodeWith(new FilterRule("Show Mythics", Visibility.Show, 0, []));
        var overriddenByCode = CodeWith(new FilterRule("Hide Rares Later", Visibility.Recolor, 0, []));
        vm.OverrideBlockCode = overrideCode;
        vm.OverriddenByBlockCode = overriddenByCode;

        vm.GenerateCommand.Execute(null);

        var stored = store.LoadAll().Profiles.Single();
        stored.OverrideBlockCode.ShouldBe(overrideCode);
        stored.OverriddenByBlockCode.ShouldBe(overriddenByCode);
    }

    [Fact]
    public async Task StartNewProfile_clears_block_codes()
    {
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles[0];
        vm.OpenSelectedProfileCommand.Execute(null);
        vm.OverrideBlockCode = CodeWith(new FilterRule("Show Mythics", Visibility.Show, 0, []));
        vm.OverriddenByBlockCode = CodeWith(new FilterRule("Hide Rares Later", Visibility.Recolor, 0, []));

        vm.StartNewProfileCommand.Execute(null);

        vm.OverrideBlockCode.ShouldBe("");
        vm.OverriddenByBlockCode.ShouldBe("");
    }

    [Fact]
    public async Task SaveAsProfile_EmptyBlockCodes_PersistAsNull()
    {
        // SnapshotCurrentState maps "" → null for both block codes so the on-disk schema distinguishes
        // "never set" from "set to empty" (ProfileSerializer preserves both distinctly). This exercises
        // that ternary through the real SaveAsProfile command path, not just a hand-built domain record.
        var store = NewStore();
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        await vm.AddGearFromImageAsync(Stream.Null);
        vm.NextToReviewCommand.Execute(null);
        vm.SaveAsName = "P1";

        vm.SaveAsProfileCommand.Execute(null);

        var stored = store.LoadAll().Profiles.Single();
        stored.OverrideBlockCode.ShouldBeNull();
        stored.OverriddenByBlockCode.ShouldBeNull();
    }

    [Fact]
    public async Task OpenSelectedProfile_NullBlockCodes_RestoreAsEmptyString()
    {
        // Complements SaveAndOpen_roundtrips_block_codes (non-null case): a profile persisted with null
        // block codes (the common case — SavedProfileAsync never sets them) must restore as "" on open,
        // per OpenSelectedProfile's `SelectedProfile.OverrideBlockCode ?? ""` fallback.
        var store = NewStore();
        var seeded = await SavedProfileAsync(store);
        seeded.OverrideBlockCode.ShouldBeNull(); // sanity: confirms the seed actually persisted as null
        seeded.OverriddenByBlockCode.ShouldBeNull();
        var vm = NewVm(store, new FakeGearReader(HelmLines));

        vm.SelectedProfile = vm.Profiles[0];
        vm.OpenSelectedProfileCommand.Execute(null);

        vm.OverrideBlockCode.ShouldBe("");
        vm.OverriddenByBlockCode.ShouldBe("");
    }

    [Fact]
    public async Task Generate_WithActiveProfile_StaticBlockExceedsRuleCap_SurfacesValidationErrorButStillAdvancesAndAutoSaves()
    {
        // Highest-risk validation-surfacing path: an override block alone big enough to blow the 25-rule
        // budget must still produce a merged/encoded filter, land Warnings/HasError from Validate(), AND
        // still advance to Result and auto-save the active profile — a failed *generate* (bad guide text)
        // must not advance/save, but an over-budget *result* is a warning state, not a hard failure.
        var store = NewStore();
        await SavedProfileAsync(store);
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        vm.SelectedProfile = vm.Profiles[0];
        vm.OpenSelectedProfileCommand.Execute(null); // active profile, lands on Review (gear present)
        var oversizedOverride = Enumerable.Range(1, 25)
            .Select(i => new FilterRule($"O{i}", Visibility.Show, 0, []))
            .ToArray();
        vm.OverrideBlockCode = CodeWith(oversizedOverride);

        vm.GenerateCommand.Execute(null);

        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldContain("validation error");
        vm.Warnings.ShouldContain(w => w.Contains("exceeding the", StringComparison.Ordinal));
        vm.Warnings.ShouldContain(w => w.Contains("maximum is 25", StringComparison.Ordinal));
        vm.ShareCode.ShouldNotBeNullOrEmpty(); // still encodes/exports despite the validation error
        vm.CurrentStep.ShouldBe(ProgressionStep.Result); // still advances

        var stored = store.LoadAll().Profiles.Single();
        stored.OverrideBlockCode.ShouldBe(vm.OverrideBlockCode); // auto-save still ran
    }

    private ProfileStore NewStore(Func<DateTimeOffset>? clock = null) => new(_dir, clock);

    private static Func<DateTimeOffset> MonotonicClock()
    {
        var t = DateTimeOffset.UnixEpoch;
        return () => t = t.AddSeconds(1);
    }

    // Builds a VM sharing a caller-supplied store so tests can seed and inspect the same directory.
    private static ProgressionWizardViewModel NewVm(
        ProfileStore store, IGearReader reader, Func<string, bool>? confirm = null)
    {
        var data = new FilterDataService();
        var resolver = new NameResolver(data);
        var roleMap = new WeaponRoleMap(resolver);
        return new ProgressionWizardViewModel(
            reader,
            new GearTooltipParser(data),
            new BuildGuideImporter(),
            new GoalBuildFactory(resolver, roleMap),
            new SlotDiffEngine(),
            new ProgressionFilterGenerator(resolver, roleMap),
            new ProgressionFilterMerger(),
            roleMap,
            store,
            setClipboard: null,
            confirm: confirm ?? (_ => true));
    }

    // Encodes a throwaway one-off ruleset into a share code so block-code round-trip tests have real codes.
    private static string CodeWith(params FilterRule[] rules)
        => FilterCodec.Encode(new FilterRuleset("Block", rules));

    // Persists a single verified-helm profile named P1 into the store via the real save-as flow.
    private static async Task<ProgressionProfile> SavedProfileAsync(ProfileStore store, string name = "P1")
    {
        var vm = NewVm(store, new FakeGearReader(HelmLines));
        await vm.AddGearFromImageAsync(Stream.Null);
        vm.PastedText = Guide;
        vm.NextToReviewCommand.Execute(null);
        vm.SaveAsName = name;
        vm.SaveAsProfileCommand.Execute(null);
        return store.LoadAll().Profiles.Single(p => p.Name == name);
    }
}

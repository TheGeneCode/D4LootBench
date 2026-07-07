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

public sealed class ProgressionWizardViewModelTests
{
    // Maxroll-style guide (first line is a slot keyword → auto-detects) with Helm + Gloves both
    // targeting the helm's affixes.
    private const string Guide =
        "Helm\nCritical Strike Chance\nMaximum Life\nCooldown Reduction\n" +
        "Gloves\nCritical Strike Chance\nMaximum Life\nCooldown Reduction\n";

    // Maxroll-style guide with a bare "Weapon" header (recognized slot keyword). For a class-aware run the
    // header maps to the class's weapon role: for Barbarian → Mainhand → the barb one-handers (multi-type).
    private const string WeaponGuide =
        "Weapon\nCritical Strike Chance\nMaximum Life\nCooldown Reduction\n";

    // A two-handed mace tooltip (mirrors the weapon-base-damage parser fixture) — resolves to a single
    // TwoHand weapon role, so two of them collide on one slot key.
    private static readonly IReadOnlyList<string> TwoHandMaceLines =
    [
        "Crimson Crude Hammer of Steel",
        "Magic Two-Handed Mace (Bludgeoning)",
        "850 Item Power",
        "+287 Weapon Damage [187 - 312]",
    ];

    // Mirrors the legendary-helm parser fixture: slot Helm + three resolvable affixes.
    private static readonly IReadOnlyList<string> HelmLines =
    [
        "Ancestral Legendary Helm",
        "925 Item Power",
        "+45.0% Critical Strike Chance",
        "+112 Maximum Life",
        "+8.5% Cooldown Reduction",
        "Requires Level 60",
    ];

    private static ProgressionWizardViewModel NewVm(
        IReadOnlyList<string> lines, Action<string>? clipboard = null, Func<string>? getClipboard = null,
        Func<FilterRuleset, string, string?>? editBlock = null)
        => NewVmWithReader(new FakeGearReader(lines), clipboard, getClipboard, editBlock);

    private static ProgressionWizardViewModel NewVmWithReader(
        IGearReader reader, Action<string>? clipboard = null, Func<string>? getClipboard = null,
        Func<FilterRuleset, string, string?>? editBlock = null)
    {
        var data = new FilterDataService();
        var resolver = new NameResolver(data);
        var roleMap = new WeaponRoleMap(resolver);
        var store = new ProfileStore(Path.Combine(
            Path.GetTempPath(), "d4lb-wizard-tests", Guid.NewGuid().ToString("N")));
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
            clipboard,
            getClipboard,
            editBlock,
            confirm: _ => true);
    }

    // Substitutes the modal BlockEditorWindow: records the (ruleset, title) the edit seam was invoked with and
    // returns a canned result — a share code, or null to simulate Cancel — so the VM stays headless in tests.
    private sealed class FakeEditBlock(string? result)
    {
        public FilterRuleset? CapturedRuleset { get; private set; }

        public string? CapturedTitle { get; private set; }

        public int CallCount { get; private set; }

        public Func<FilterRuleset, string, string?> Seam => (ruleset, title) =>
        {
            CapturedRuleset = ruleset;
            CapturedTitle = title;
            CallCount++;
            return result;
        };
    }

    // A mutable in-memory clipboard for import tests: the Get seam returns Text, the Set seam writes it.
    private sealed class FakeClipboard
    {
        public string Text { get; set; } = "";

        public Func<string> Get => () => Text;
    }

    // Encodes a throwaway one-off ruleset into a share code so tests can craft block/import codes.
    private static string CodeWith(params FilterRule[] rules)
        => FilterCodec.Encode(new FilterRuleset("Block", rules));

    // Read failure seam: throws instead of returning OCR lines, to exercise AddGearFromImageAsync's
    // catch block without touching Windows OCR.
    private sealed class ThrowingGearReader : IGearReader
    {
        public Task<IReadOnlyList<string>> ReadLinesAsync(Stream image, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("OCR unavailable.");
    }

    [Fact]
    public async Task Wizard_read_adds_item_and_enables_next()
    {
        var vm = NewVm(HelmLines);

        await vm.AddGearFromImageAsync(new MemoryStream());

        vm.Items.Count.ShouldBe(1);
        vm.NextToReviewCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task Wizard_review_rebuilds_authoritative_session()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        await vm.AddGearFromImageAsync(new MemoryStream());

        vm.NextToReviewCommand.Execute(null);
        vm.Items[1].Slot = GearSlot.Gloves; // correct the second item's slot

        vm.PastedText = Guide;
        vm.GenerateCommand.Execute(null);

        // Corrected slot flowed into the diff: the corrected item fills the Gloves slot and is maxed on its
        // targets, so the slot emits at most a cyan "Gloves (Greater)" rule — never a pink "Gloves" rule. A
        // pink "Gloves" rule (empty slot → same-or-more from zero) would appear only if the edit had not
        // reached the session.
        vm.GeneratedRuleset.ShouldNotBeNull();
        vm.GeneratedRuleset!.Rules.ShouldNotContain(r => r.Name == "Gloves");
    }

    [Fact]
    public async Task Wizard_generate_produces_decodable_share_code()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;

        vm.GenerateCommand.Execute(null);

        vm.ShareCode.ShouldNotBeNullOrEmpty();
        FilterCodec.Decode(vm.ShareCode).Rules.Count.ShouldBe(vm.GeneratedRuleset!.Rules.Count);
    }

    [Fact]
    public async Task Wizard_generate_disabled_without_gear_or_text()
    {
        var vm = NewVm(HelmLines);
        vm.GenerateCommand.CanExecute(null).ShouldBeFalse();

        vm.PastedText = Guide; // text but no session yet
        vm.GenerateCommand.CanExecute(null).ShouldBeFalse();

        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.GenerateCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task Wizard_copy_uses_injected_clipboard()
    {
        string? captured = null;
        var vm = NewVm(HelmLines, s => captured = s);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.GenerateCommand.Execute(null);

        vm.CopyCodeCommand.Execute(null);

        captured.ShouldBe(vm.ShareCode);
    }

    [Fact]
    public async Task Wizard_add_gear_read_failure_reports_error_and_adds_no_item()
    {
        var vm = NewVmWithReader(new ThrowingGearReader());

        await vm.AddGearFromImageAsync(new MemoryStream());

        vm.Items.ShouldBeEmpty();
        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldContain("Read failed");
        vm.NextToReviewCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task Wizard_remove_item_not_in_collection_is_noop()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        var detachedSession = new GearReviewSession([new GearTooltipParser(new FilterDataService()).Parse(HelmLines)]);
        var detached = new GearItemDraftViewModel(detachedSession.Items[0]);

        vm.RemoveItemCommand.Execute(detached);

        vm.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Wizard_remove_item_keeps_parsed_list_in_index_sync()
    {
        var lines925 = HelmLines;
        var lines111 = HelmLines.Select(l => l == "925 Item Power" ? "111 Item Power" : l).ToList();
        var lines222 = HelmLines.Select(l => l == "925 Item Power" ? "222 Item Power" : l).ToList();
        var vm = NewVmWithReader(new SequenceGearReader(lines925, lines111, lines222));

        await vm.AddGearFromImageAsync(new MemoryStream());
        await vm.AddGearFromImageAsync(new MemoryStream());
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.Items.Select(i => i.ItemPower).ShouldBe(new int?[] { 925, 111, 222 });

        vm.RemoveItemCommand.Execute(vm.Items[1]); // remove the middle (111) item
        vm.Items.Select(i => i.ItemPower).ShouldBe(new int?[] { 925, 222 });

        // Rebuild the authoritative session from _parsed; if RemoveItem hadn't kept _parsed in index
        // sync with Items, the removed 111 item would reappear here.
        vm.NextToReviewCommand.Execute(null);

        vm.Items.Select(i => i.ItemPower).ShouldBe(new int?[] { 925, 222 });
    }

    [Fact]
    public async Task Wizard_removing_item_after_review_invalidates_session_for_generate()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.GenerateCommand.CanExecute(null).ShouldBeTrue();

        vm.RemoveItemCommand.Execute(vm.Items[0]);

        vm.GenerateCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task Wizard_generate_reports_error_on_undetectable_guide_format()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.NextToGoalCommand.Execute(null);
        vm.PastedText = "this text matches no known build-guide format at all";

        vm.GenerateCommand.Execute(null);

        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldContain("Format could not be detected");
        vm.ShareCode.ShouldBeEmpty();
        vm.CurrentStep.ShouldBe(ProgressionStep.Goal); // stays put — failed generate doesn't advance to Result
    }

    [Fact]
    public void Wizard_defaults_to_All_class()
    {
        var vm = NewVm(HelmLines);

        vm.SelectedClass.ShouldBe(PlayerClass.All);
        ProgressionWizardViewModel.Classes.ShouldContain(PlayerClass.Barbarian);
    }

    [Fact]
    public async Task Wizard_barb_generates_multi_type_weapon_rule()
    {
        var vm = NewVm(HelmLines); // equipped: a helm only — the weapon slot is empty, so it needs a rule
        vm.SelectedClass = PlayerClass.Barbarian;
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = WeaponGuide;

        vm.GenerateCommand.Execute(null);

        vm.HasError.ShouldBeFalse();
        vm.ShareCode.ShouldNotBeNullOrEmpty();
        vm.GeneratedRuleset.ShouldNotBeNull();

        // The class-aware weapon role expands to the concrete barb one-hander types (>1). Assert both the
        // multi-type shape and that the exact hash set matches the Barbarian mainhand role — so a regression
        // that dropped class threading (defaulting to All, a different set) would fail here too.
        var expected = new WeaponRoleMap(new NameResolver(new FilterDataService()))
            .AllowedTypeHashes(WeaponSlotRole.Mainhand, PlayerClass.Barbarian);
        var typeIds = vm.GeneratedRuleset!.Rules
            .Where(r => r.Visibility == Visibility.Recolor)
            .SelectMany(r => r.Conditions.OfType<ItemTypeCondition>())
            .Single()
            .TypeIds;
        typeIds.Count.ShouldBeGreaterThan(1);
        typeIds.ShouldBe(expected);
    }

    [Fact]
    public async Task Wizard_all_class_weapon_types_are_superset_of_barbarian()
    {
        var vmAll = NewVm(HelmLines); // SelectedClass defaults to All
        await vmAll.AddGearFromImageAsync(new MemoryStream());
        vmAll.NextToReviewCommand.Execute(null);
        vmAll.PastedText = WeaponGuide;
        vmAll.GenerateCommand.Execute(null);

        var vmBarb = NewVm(HelmLines);
        vmBarb.SelectedClass = PlayerClass.Barbarian;
        await vmBarb.AddGearFromImageAsync(new MemoryStream());
        vmBarb.NextToReviewCommand.Execute(null);
        vmBarb.PastedText = WeaponGuide;
        vmBarb.GenerateCommand.Execute(null);

        var allTypeIds = vmAll.GeneratedRuleset!.Rules
            .Where(r => r.Visibility == Visibility.Recolor)
            .SelectMany(r => r.Conditions.OfType<ItemTypeCondition>())
            .Single()
            .TypeIds;
        var barbTypeIds = vmBarb.GeneratedRuleset!.Rules
            .Where(r => r.Visibility == Visibility.Recolor)
            .SelectMany(r => r.Conditions.OfType<ItemTypeCondition>())
            .Single()
            .TypeIds;

        // All = class-agnostic union across every class's one-handers; must be a strict superset of
        // Barbarian's own mainhand set, not merely a differently-shaped set.
        barbTypeIds.All(h => allTypeIds.Contains(h)).ShouldBeTrue();
        allTypeIds.Count.ShouldBeGreaterThan(barbTypeIds.Count);
    }

    [Fact]
    public async Task Wizard_spiritborn_weapon_role_empty_emits_affix_only_rule_with_warning()
    {
        // Spiritborn has no catalog one-handed "Mainhand" weapon types (fists/focus-based kit) — the
        // class-aware role map legitimately resolves to an empty type set for this class. Regenerating
        // must not crash or silently gate on zero types; it should fall back to an affix-only rule and
        // surface the "ambiguous item type" warning (per ProgressionFilterGenerator.ResolveTypeHashes).
        var vm = NewVm(HelmLines);
        vm.SelectedClass = PlayerClass.Spiritborn;
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = WeaponGuide;

        vm.GenerateCommand.Execute(null);

        vm.HasError.ShouldBeFalse();
        vm.ShareCode.ShouldNotBeNullOrEmpty();
        vm.Warnings.ShouldContain(w => w.Contains("Ambiguous item type", StringComparison.Ordinal));
        var weaponRule = vm.GeneratedRuleset!.Rules.Single(r => r.Visibility == Visibility.Recolor);
        weaponRule.Conditions.OfType<ItemTypeCondition>().ShouldBeEmpty();
        weaponRule.Conditions.OfType<AffixCondition>().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Wizard_regenerate_after_class_change_reflects_new_class_not_stale()
    {
        var vm = NewVm(HelmLines);
        vm.SelectedClass = PlayerClass.Barbarian;
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = WeaponGuide;
        vm.GenerateCommand.Execute(null);
        var barbTypeIds = vm.GeneratedRuleset!.Rules
            .Where(r => r.Visibility == Visibility.Recolor)
            .SelectMany(r => r.Conditions.OfType<ItemTypeCondition>())
            .Single()
            .TypeIds;

        // Same session, no re-read of gear — only the class changes before regenerating. Verifies
        // Generate() reads SelectedClass live each call rather than caching the first class's result.
        vm.SelectedClass = PlayerClass.Sorcerer;
        vm.GenerateCommand.Execute(null);

        vm.HasError.ShouldBeFalse();
        var sorcTypeIds = vm.GeneratedRuleset!.Rules
            .Where(r => r.Visibility == Visibility.Recolor)
            .SelectMany(r => r.Conditions.OfType<ItemTypeCondition>())
            .Single()
            .TypeIds;
        sorcTypeIds.ShouldNotBe(barbTypeIds);
    }

    [Fact]
    public async Task Wizard_class_selection_does_not_affect_non_weapon_slot_rules()
    {
        // Guide has no weapon header (Helm + Gloves only) — class threading must be scoped to
        // weapon/offhand slots and leave ordinary armor-slot rule generation untouched.
        var vmAll = NewVm(HelmLines);
        await vmAll.AddGearFromImageAsync(new MemoryStream());
        vmAll.NextToReviewCommand.Execute(null);
        vmAll.PastedText = Guide;
        vmAll.GenerateCommand.Execute(null);

        var vmBarb = NewVm(HelmLines);
        vmBarb.SelectedClass = PlayerClass.Barbarian;
        await vmBarb.AddGearFromImageAsync(new MemoryStream());
        vmBarb.NextToReviewCommand.Execute(null);
        vmBarb.PastedText = Guide;
        vmBarb.GenerateCommand.Execute(null);

        vmBarb.GeneratedRuleset!.Rules.Select(r => r.Name)
            .ShouldBe(vmAll.GeneratedRuleset!.Rules.Select(r => r.Name));
        vmBarb.ShareCode.ShouldBe(vmAll.ShareCode);
    }

    [Fact]
    public async Task Wizard_class_change_notifies_and_stays_generatable()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = WeaponGuide;

        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProgressionWizardViewModel.SelectedClass))
            {
                raised = true;
            }
        };
        vm.SelectedClass = PlayerClass.Barbarian;

        raised.ShouldBeTrue();
        vm.GenerateCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task Wizard_generate_surfaces_duplicate_slot_collision_warning()
    {
        // Same two-handed weapon snipped twice → both items resolve to the class's single TwoHand slot,
        // so FromItems drops the first last-wins. Generate must surface that as a user-visible warning.
        var vm = NewVmWithReader(new SequenceGearReader(TwoHandMaceLines, TwoHandMaceLines));
        vm.SelectedClass = PlayerClass.Barbarian;
        await vm.AddGearFromImageAsync(new MemoryStream());
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = WeaponGuide;

        vm.GenerateCommand.Execute(null);

        vm.HasError.ShouldBeFalse();
        vm.Warnings.ShouldContain(w => w.Contains("same", StringComparison.Ordinal) && w.Contains("slot", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Wizard_start_over_resets_all_state()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.GenerateCommand.Execute(null);
        vm.ShareCode.ShouldNotBeNullOrEmpty(); // sanity: generation happened before reset

        vm.StartOverCommand.Execute(null);

        vm.Items.ShouldBeEmpty();
        vm.PastedText.ShouldBeEmpty();
        vm.ShareCode.ShouldBeEmpty();
        vm.Warnings.ShouldBeEmpty();
        vm.GeneratedRuleset.ShouldBeNull();
        vm.CurrentStep.ShouldBe(ProgressionStep.Profiles); // StartOver now lands on the Profiles home
        vm.NextToReviewCommand.CanExecute(null).ShouldBeFalse();
        vm.GenerateCommand.CanExecute(null).ShouldBeFalse();
    }

    // ── Static-block merge + clipboard import/clear (Phase 2) ────────────────

    [Fact]
    public async Task Generate_with_empty_blocks_matches_prior_shape()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;

        vm.GenerateCommand.Execute(null);

        // With no static blocks the merged output keeps the pre-feature shape: exactly one Hide-All, and it's
        // the last rule (the generator's trailing catch-all, swapped for a fresh one by the merger).
        var decoded = FilterCodec.Decode(vm.ShareCode);
        decoded.Rules.Count(r => r.Visibility == Visibility.HideAll).ShouldBe(1);
        decoded.Rules[^1].Visibility.ShouldBe(Visibility.HideAll);
        decoded.Rules.Count.ShouldBe(vm.GeneratedRuleset!.Rules.Count);
    }

    [Fact]
    public async Task Generate_with_override_block_places_override_first()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.OverrideBlockCode = CodeWith(new FilterRule("Show Mythics", Visibility.Show, 0, []));

        vm.GenerateCommand.Execute(null);

        var decoded = FilterCodec.Decode(vm.ShareCode);
        decoded.Rules[0].Name.ShouldBe("Show Mythics"); // override sits above the better-gear rules
        decoded.Rules.Count.ShouldBeGreaterThan(2);      // ...which still follow it
        decoded.Rules[^1].Visibility.ShouldBe(Visibility.HideAll);
    }

    [Fact]
    public async Task Generate_with_overriddenby_block_places_it_before_final_hideall()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.OverriddenByBlockCode = CodeWith(new FilterRule("Hide Rares Later", Visibility.Recolor, 0, []));

        vm.GenerateCommand.Execute(null);

        var decoded = FilterCodec.Decode(vm.ShareCode);
        decoded.Rules[^1].Visibility.ShouldBe(Visibility.HideAll); // final catch-all
        decoded.Rules[^2].Name.ShouldBe("Hide Rares Later");       // after better-gear, before the catch-all
    }

    [Fact]
    public void Import_override_from_clipboard_sets_code_and_count()
    {
        var clip = new FakeClipboard
        {
            Text = CodeWith(
                new FilterRule("A", Visibility.Show, 0, []),
                new FilterRule("B", Visibility.Show, 0, [])),
        };
        var vm = NewVm(HelmLines, getClipboard: clip.Get);

        vm.ImportOverrideBlockCommand.Execute(null);

        vm.OverrideBlockCode.ShouldBe(clip.Text);
        vm.OverrideRuleCount.ShouldBe(2);
        vm.HasError.ShouldBeFalse();
    }

    [Fact]
    public void Import_invalid_code_reports_error_and_leaves_block()
    {
        var clip = new FakeClipboard { Text = "not base64" };
        var vm = NewVm(HelmLines, getClipboard: clip.Get);

        vm.ImportOverrideBlockCommand.Execute(null);

        vm.OverrideBlockCode.ShouldBe(""); // untouched on failure
        vm.HasError.ShouldBeTrue();
    }

    [Fact]
    public void Import_empty_clipboard_reports_error()
    {
        var clip = new FakeClipboard { Text = "" };
        var vm = NewVm(HelmLines, getClipboard: clip.Get);

        vm.ImportOverrideBlockCommand.Execute(null);

        vm.HasError.ShouldBeTrue();
        vm.OverrideBlockCode.ShouldBe("");
    }

    [Fact]
    public void Clear_override_block_empties_code()
    {
        var vm = NewVm(HelmLines);
        vm.OverrideBlockCode = CodeWith(new FilterRule("A", Visibility.Show, 0, []));
        vm.OverrideRuleCount.ShouldBe(1); // sanity

        vm.ClearOverrideBlockCommand.Execute(null);

        vm.OverrideBlockCode.ShouldBe("");
        vm.OverrideRuleCount.ShouldBe(0);
    }

    [Fact]
    public async Task Generated_merged_code_round_trips()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.OverrideBlockCode = CodeWith(new FilterRule("Show Mythics", Visibility.Show, 0, []));
        vm.OverriddenByBlockCode = CodeWith(new FilterRule("Hide Rares Later", Visibility.Recolor, 0, []));

        vm.GenerateCommand.Execute(null);

        FilterCodec.Encode(FilterCodec.Decode(vm.ShareCode)).ShouldBe(vm.ShareCode);
    }

    // ── Additional QA coverage: import/clear symmetry, property-change wiring, HideAll-in-block
    //    through the real wizard Generate path, validation-cap surfacing, and null/empty persistence ──

    [Fact]
    public void Import_overriddenby_replaces_previous_code_in_same_block_only()
    {
        // The isOverride:false arm of ImportBlockFromClipboard had zero direct test coverage — only the
        // override arm was exercised. Also covers re-import replacing a prior value in the same block.
        var firstCode = CodeWith(new FilterRule("First", Visibility.Show, 0, []));
        var secondCode = CodeWith(
            new FilterRule("Second", Visibility.Show, 0, []),
            new FilterRule("Second2", Visibility.Show, 0, []));
        var clip = new FakeClipboard { Text = firstCode };
        var vm = NewVm(HelmLines, getClipboard: clip.Get);

        vm.ImportOverriddenByBlockCommand.Execute(null);
        vm.OverriddenByBlockCode.ShouldBe(firstCode);
        vm.OverriddenByRuleCount.ShouldBe(1);

        clip.Text = secondCode;
        vm.ImportOverriddenByBlockCommand.Execute(null);

        vm.OverriddenByBlockCode.ShouldBe(secondCode);
        vm.OverriddenByRuleCount.ShouldBe(2);
        vm.HasError.ShouldBeFalse();
    }

    [Fact]
    public void Import_invalid_code_into_overriddenby_reports_error_and_leaves_existing_block_untouched()
    {
        // Stronger than the mirrored override-block test: a *pre-existing non-empty* block must survive a
        // failed re-import unchanged, not merely stay empty because nothing was set before.
        var existing = CodeWith(new FilterRule("Existing", Visibility.Show, 0, []));
        var clip = new FakeClipboard { Text = "not base64" };
        var vm = NewVm(HelmLines, getClipboard: clip.Get);
        vm.OverriddenByBlockCode = existing;

        vm.ImportOverriddenByBlockCommand.Execute(null);

        vm.OverriddenByBlockCode.ShouldBe(existing);
        vm.HasError.ShouldBeTrue();
    }

    [Fact]
    public void Import_whitespace_only_clipboard_reports_empty_error()
    {
        // ImportBlockFromClipboard trims before checking IsNullOrEmpty; a whitespace-only clipboard must
        // collapse to the same "empty" error path as a truly empty clipboard, not attempt to decode " \t ".
        var clip = new FakeClipboard { Text = "   \t  " };
        var vm = NewVm(HelmLines, getClipboard: clip.Get);

        vm.ImportOverrideBlockCommand.Execute(null);

        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldContain("Clipboard is empty");
        vm.OverrideBlockCode.ShouldBe("");
    }

    [Fact]
    public void Clear_overriddenby_block_empties_code()
    {
        var vm = NewVm(HelmLines);
        vm.OverriddenByBlockCode = CodeWith(new FilterRule("X", Visibility.Recolor, 0, []));
        vm.OverriddenByRuleCount.ShouldBe(1); // sanity

        vm.ClearOverriddenByBlockCommand.Execute(null);

        vm.OverriddenByBlockCode.ShouldBe("");
        vm.OverriddenByRuleCount.ShouldBe(0);
    }

    [Fact]
    public void Import_into_override_block_leaves_overriddenby_block_untouched()
    {
        var existingOverriddenBy = CodeWith(new FilterRule("X", Visibility.Recolor, 0, []));
        var clip = new FakeClipboard { Text = CodeWith(new FilterRule("A", Visibility.Show, 0, [])) };
        var vm = NewVm(HelmLines, getClipboard: clip.Get);
        vm.OverriddenByBlockCode = existingOverriddenBy;

        vm.ImportOverrideBlockCommand.Execute(null);

        vm.OverrideBlockCode.ShouldBe(clip.Text);
        vm.OverriddenByBlockCode.ShouldBe(existingOverriddenBy); // untouched by an import into the other block
    }

    [Fact]
    public void OverrideBlockCode_change_raises_PropertyChanged_for_code_and_derived_rule_count()
    {
        // UI binding correctness: OverrideRuleCount is derived and only refreshed via the explicit
        // OnOverrideBlockCodeChanged → OnPropertyChanged(nameof(OverrideRuleCount)) wiring. Reading the
        // value after the fact (as other tests do) can't catch a missing/removed notification — only
        // subscribing to PropertyChanged can.
        var vm = NewVm(HelmLines);
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.OverrideBlockCode = CodeWith(new FilterRule("A", Visibility.Show, 0, []));

        raised.ShouldContain(nameof(ProgressionWizardViewModel.OverrideBlockCode));
        raised.ShouldContain(nameof(ProgressionWizardViewModel.OverrideRuleCount));
    }

    [Fact]
    public void OverriddenByBlockCode_change_raises_PropertyChanged_for_code_and_derived_rule_count()
    {
        var vm = NewVm(HelmLines);
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.OverriddenByBlockCode = CodeWith(new FilterRule("A", Visibility.Show, 0, []));

        raised.ShouldContain(nameof(ProgressionWizardViewModel.OverriddenByBlockCode));
        raised.ShouldContain(nameof(ProgressionWizardViewModel.OverriddenByRuleCount));
    }

    [Fact]
    public async Task Generate_with_override_block_ending_in_hideall_drops_it_and_surfaces_warning_through_wizard()
    {
        // Merger unit tests already cover this in isolation; this exercises the same hazard through the
        // real VM wiring (DecodeBlock → Merge → FilterCodec.Encode) to catch wiring bugs (e.g. a swapped
        // override/overridden-by argument) that pure Merger-level tests cannot see.
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.OverrideBlockCode = CodeWith(
            new FilterRule("Show Mythics", Visibility.Show, 0, []),
            new FilterRule("Trailing Hide", Visibility.HideAll, 0, []));

        vm.GenerateCommand.Execute(null);

        vm.HasError.ShouldBeFalse();
        vm.Warnings.ShouldContain(w => w.Contains("override block", StringComparison.Ordinal));
        var decoded = FilterCodec.Decode(vm.ShareCode);
        decoded.Rules.ShouldNotContain(r => r.Name == "Trailing Hide");
        decoded.Rules[0].Name.ShouldBe("Show Mythics");
        decoded.Rules.Count(r => r.Visibility == Visibility.HideAll).ShouldBe(1);
        decoded.Rules[^1].Visibility.ShouldBe(Visibility.HideAll);
    }

    [Fact]
    public async Task Generate_with_overriddenby_block_ending_in_hideall_becomes_final_catchall_through_wizard()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.OverriddenByBlockCode = CodeWith(
            new FilterRule("Hide Rares Later", Visibility.Recolor, 0, []),
            new FilterRule("Custom Catchall", Visibility.HideAll, 5, []));

        vm.GenerateCommand.Execute(null);

        vm.HasError.ShouldBeFalse();
        var decoded = FilterCodec.Decode(vm.ShareCode);
        decoded.Rules.Count(r => r.Visibility == Visibility.HideAll).ShouldBe(1);
        decoded.Rules[^1].Name.ShouldBe("Custom Catchall"); // the block's own trailing Hide-All, not a fresh one
        decoded.Rules[^2].Name.ShouldBe("Hide Rares Later");
    }

    // ── Static-block manual "Edit…" seam (Phase 3) ──────────────────────────

    [Fact]
    public void EditOverrideBlock_applies_returned_code()
    {
        var returned = CodeWith(
            new FilterRule("R1", Visibility.Show, 0, []),
            new FilterRule("R2", Visibility.Show, 0, []),
            new FilterRule("R3", Visibility.Show, 0, []));
        var edit = new FakeEditBlock(returned);
        var vm = NewVm(HelmLines, editBlock: edit.Seam);

        vm.EditOverrideBlockCommand.Execute(null);

        vm.OverrideBlockCode.ShouldBe(returned);
        vm.OverrideRuleCount.ShouldBe(3);
        vm.HasError.ShouldBeFalse();
    }

    [Fact]
    public void EditOverrideBlock_cancel_leaves_code_unchanged()
    {
        var existing = CodeWith(new FilterRule("Existing", Visibility.Show, 0, []));
        var edit = new FakeEditBlock(null); // null result == Cancel
        var vm = NewVm(HelmLines, editBlock: edit.Seam);
        vm.OverrideBlockCode = existing;

        vm.EditOverrideBlockCommand.Execute(null);

        vm.OverrideBlockCode.ShouldBe(existing); // untouched on cancel
        vm.HasError.ShouldBeFalse();
    }

    [Fact]
    public void EditOverrideBlock_from_empty_seeds_empty_ruleset()
    {
        var edit = new FakeEditBlock(null);
        var vm = NewVm(HelmLines, editBlock: edit.Seam);
        vm.OverrideBlockCode.ShouldBe(""); // sanity: no block yet

        vm.EditOverrideBlockCommand.Execute(null);

        edit.CapturedRuleset.ShouldNotBeNull();
        edit.CapturedRuleset!.Rules.ShouldBeEmpty();
        edit.CapturedRuleset.Name.ShouldBe("Override Rules");
    }

    [Fact]
    public void EditOverriddenByBlock_from_existing_seeds_decoded_rules()
    {
        var existing = CodeWith(
            new FilterRule("Alpha", Visibility.Recolor, 0, []),
            new FilterRule("Beta", Visibility.Recolor, 0, []));
        var edit = new FakeEditBlock(null);
        var vm = NewVm(HelmLines, editBlock: edit.Seam);
        vm.OverriddenByBlockCode = existing;

        vm.EditOverriddenByBlockCommand.Execute(null);

        edit.CapturedRuleset.ShouldNotBeNull();
        edit.CapturedRuleset!.Rules.Select(r => r.Name).ShouldBe(new[] { "Alpha", "Beta" });
    }

    [Fact]
    public void EditOverriddenByBlock_applies_returned_code()
    {
        var returned = CodeWith(
            new FilterRule("One", Visibility.Recolor, 0, []),
            new FilterRule("Two", Visibility.Recolor, 0, []));
        var edit = new FakeEditBlock(returned);
        var vm = NewVm(HelmLines, editBlock: edit.Seam);

        vm.EditOverriddenByBlockCommand.Execute(null);

        vm.OverriddenByBlockCode.ShouldBe(returned);
        vm.OverriddenByRuleCount.ShouldBe(2);
        vm.HasError.ShouldBeFalse();
    }

    // ── EditBlock corrupt-code guard (found via QA review of the Phase 3 handoff) ───────────
    // EditBlock originally called FilterCodec.Decode(code) with no try/catch, unlike
    // ImportBlockFromClipboard/CountRules. A block code can reach EditBlock without ever having passed
    // through Import's validation — OpenSelectedProfile assigns OverrideBlockCode/OverriddenByBlockCode
    // straight from a persisted profile (ProfileStore does no validation on these fields), so a hand-edited
    // or corrupted profile JSON produces an undecodable code that would previously crash the app when the
    // user clicked "Edit...". These tests simulate that by writing directly to the block property (bypassing
    // Import), which is exactly what OpenSelectedProfile does.

    [Fact]
    public void EditOverrideBlock_with_corrupt_stored_code_reports_error_and_does_not_open_editor()
    {
        var edit = new FakeEditBlock("should never be used");
        var vm = NewVm(HelmLines, editBlock: edit.Seam);
        vm.OverrideBlockCode = "not base64"; // simulates a profile-restored/corrupted code, bypassing Import

        vm.EditOverrideBlockCommand.Execute(null);

        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldContain("Cannot edit the override block");
        edit.CallCount.ShouldBe(0); // never opens the editor on an undecodable block
        vm.OverrideBlockCode.ShouldBe("not base64"); // left untouched, not clobbered/cleared
    }

    [Fact]
    public void EditOverriddenByBlock_with_corrupt_stored_code_reports_error_and_does_not_open_editor()
    {
        var edit = new FakeEditBlock("should never be used");
        var vm = NewVm(HelmLines, editBlock: edit.Seam);
        vm.OverriddenByBlockCode = "not base64";

        vm.EditOverriddenByBlockCommand.Execute(null);

        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldContain("Cannot edit the overridden-by block");
        edit.CallCount.ShouldBe(0);
        vm.OverriddenByBlockCode.ShouldBe("not base64");
    }

    [Fact]
    public void EditOverrideBlock_editor_returns_empty_ruleset_zeroes_rule_count()
    {
        // User deleted every rule in the editor, then OK'd — the returned code encodes zero rules. The
        // block must accept it (this is a legitimate "clear the block via editing" outcome, distinct from
        // Cancel/null) and OverrideRuleCount must read 0, not stay stale or throw.
        var existing = CodeWith(new FilterRule("Existing", Visibility.Show, 0, []));
        var emptyReturn = CodeWith(); // encodes a 0-rule ruleset
        var edit = new FakeEditBlock(emptyReturn);
        var vm = NewVm(HelmLines, editBlock: edit.Seam);
        vm.OverrideBlockCode = existing;

        vm.EditOverrideBlockCommand.Execute(null);

        vm.OverrideBlockCode.ShouldBe(emptyReturn);
        vm.OverrideRuleCount.ShouldBe(0);
        vm.HasError.ShouldBeFalse();
        FilterCodec.Decode(vm.OverrideBlockCode).Rules.ShouldBeEmpty();
    }

    [Fact]
    public void EditOverrideBlock_round_trip_preserves_conditions_not_just_rule_count()
    {
        // Stronger than EditOverrideBlock_applies_returned_code (which only checks count): confirms the
        // applied code is the seam's result assigned verbatim (not re-encoded/mutated by the VM) and that
        // a rule's conditions survive the decode → editor → encode → decode path used in production.
        var ruleWithCondition = new FilterRule(
            "Conditioned",
            Visibility.Recolor,
            0,
            [new ItemPowerCondition(700, 925)]);
        var returned = FilterCodec.Encode(new FilterRuleset("Block", [ruleWithCondition]));
        var edit = new FakeEditBlock(returned);
        var vm = NewVm(HelmLines, editBlock: edit.Seam);

        vm.EditOverrideBlockCommand.Execute(null);

        vm.OverrideBlockCode.ShouldBe(returned); // assigned verbatim
        var decodedRule = FilterCodec.Decode(vm.OverrideBlockCode).Rules.Single();
        decodedRule.Name.ShouldBe("Conditioned");
        var ip = decodedRule.Conditions.OfType<ItemPowerCondition>().Single();
        ip.Minimum.ShouldBe(700);
        ip.Maximum.ShouldBe(925);
    }
}

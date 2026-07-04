using D4LootBench.App.ViewModels.Progression;
using D4LootBench.Core.Codec;
using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
using D4LootBench.Core.Models;
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
        IReadOnlyList<string> lines, Action<string>? clipboard = null)
        => NewVmWithReader(new FakeGearReader(lines), clipboard);

    private static ProgressionWizardViewModel NewVmWithReader(
        IGearReader reader, Action<string>? clipboard = null)
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
            roleMap,
            clipboard);
    }

    // Read failure seam: throws instead of returning OCR lines, to exercise AddGearFromImageAsync's
    // catch block without touching Windows OCR.
    private sealed class ThrowingGearReader : IGearReader
    {
        public Task<IReadOnlyList<string>> ReadLinesAsync(Stream image, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("OCR unavailable.");
    }

    // Returns a different line set on each call, so successive AddGearFromImageAsync calls produce
    // distinguishable items (used to verify RemoveItem keeps Items/_parsed in index sync).
    private sealed class SequenceGearReader(params IReadOnlyList<string>[] lineSets) : IGearReader
    {
        private int _index;

        public Task<IReadOnlyList<string>> ReadLinesAsync(Stream image, CancellationToken cancellationToken = default)
            => Task.FromResult(lineSets[_index++]);
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
        // targets, so the slot emits at most a cyan "Gloves (Greater)" rule — never a gold "Gloves" rule. A
        // gold "Gloves" rule (empty slot → same-or-more from zero) would appear only if the edit had not
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
        vm.CurrentStep.ShouldBe(ProgressionStep.ReadGear);
        vm.NextToReviewCommand.CanExecute(null).ShouldBeFalse();
        vm.GenerateCommand.CanExecute(null).ShouldBeFalse();
    }
}

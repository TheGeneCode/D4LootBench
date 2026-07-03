namespace D4LootBench.Core.Tests.Progression;

using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
using D4LootBench.Core.Progression;
using Shouldly;

public sealed class GoalBuildFactoryTests
{
    private static GoalBuildFactory NewFactory()
    {
        var resolver = new NameResolver(new FilterDataService());
        return new(resolver, new WeaponRoleMap(resolver));
    }

    private static ParsedBuildGuide Guide(params ParsedSlot[] slots) => new() { Slots = [.. slots] };

    private static ParsedSlot Slot(string label, IEnumerable<string> affixes, string? itemName = null,
        bool hasUnique = false, bool talisman = false) => new()
        {
            SlotLabel = label,
            ItemName = itemName,
            HasUniqueSentinel = hasUnique,
            IsTalismanSlot = talisman,
            Affixes = affixes.Select((a, i) => new ParsedAffix { RawName = a, Priority = i + 1 }).ToList(),
        };

    private static ParsedSlot SlotWithPriorities(string label, params (string Name, int Priority)[] affixes) => new()
    {
        SlotLabel = label,
        Affixes = affixes.Select(a => new ParsedAffix { RawName = a.Name, Priority = a.Priority }).ToList(),
    };

    [Fact]
    public void EndToEnd_NoItemNameGuide_KeepsAndResolvesTopRankedAffix()
    {
        // Regression for the "missing #1 affix" bug: a low-level Maxroll slot with no item/aspect name
        // must keep its top-ranked affix (not swallow it as an item name) AND resolve it into the goal.
        const string paste = """
            Boots
            Movement Speed
            Strength
            Maximum Life
            Fury Regeneration
            Armor
            """;
        var resolver = new NameResolver(new FilterDataService());
        var guide = new BuildGuideImporter(resolver).Import(paste, BuildGuideFormat.Maxroll);
        var result = new GoalBuildFactory(resolver, new WeaponRoleMap(resolver)).Create(guide, MeetsGoalThreshold.NOf(3));

        resolver.TryResolveAffix("Movement Speed", out var movementSpeed, out _).ShouldBeTrue();
        result.GoalBuild.Goals[new SlotKey(GearSlot.Boots)].TargetAffixIds.ShouldContain(movementSpeed);
    }

    [Fact]
    public void GoalBuildFactory_resolves_slot_affixes()
    {
        var guide = Guide(Slot("Helm",
            ["Critical Strike Chance", "Maximum Life", "Cooldown Reduction"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(3));

        result.GoalBuild.Goals[new SlotKey(GearSlot.Helm)].TargetAffixIds.Count.ShouldBe(3);
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void GoalBuildFactory_stamps_chosen_threshold()
    {
        var guide = Guide(Slot("Helm", ["Critical Strike Chance", "Maximum Life"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.Exact);

        result.GoalBuild.Goals.Values.ShouldAllBe(g => g.Threshold == MeetsGoalThreshold.Exact);
    }

    [Fact]
    public void GoalBuildFactory_maps_dual_rings_to_ordinals()
    {
        var guide = Guide(
            Slot("Ring 1", ["Critical Strike Chance"]),
            Slot("Ring 2", ["Maximum Life"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(2));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Ring, 0));
        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Ring, 1));
    }

    [Fact]
    public void GoalBuildFactory_skips_talisman_slot()
    {
        var guide = Guide(Slot("Seal", ["some stat"], talisman: true));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(3));

        result.GoalBuild.Goals.ShouldBeEmpty();
        result.Warnings.ShouldContain(w => w.Contains("talisman/charm"));
    }

    [Fact]
    public void GoalBuildFactory_warns_on_unresolved_affix()
    {
        var guide = Guide(Slot("Helm",
            ["Critical Strike Chance", "zzqqxx nonsense affix"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(2));

        result.GoalBuild.Goals[new SlotKey(GearSlot.Helm)].TargetAffixIds.Count.ShouldBe(1);
        result.Warnings.ShouldContain(w => w.Contains("zzqqxx nonsense affix"));
    }

    [Fact]
    public void GoalBuildFactory_resolves_unique_via_sentinel()
    {
        var guide = Guide(Slot("Helm", ["Maximum Life"],
            itemName: "Harlequin Crest", hasUnique: true));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(3));

        result.GoalBuild.Goals[new SlotKey(GearSlot.Helm)].TargetUnique.ShouldNotBeNull();
    }

    [Fact]
    public void GoalBuildFactory_maps_slot_label_case_and_whitespace_insensitively()
    {
        var guide = Guide(Slot("  hElM  ", ["Maximum Life"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Helm));
        result.Warnings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ring")]
    [InlineData("rings")]
    [InlineData("left ring")]
    public void GoalBuildFactory_maps_bare_ring_synonyms_to_ordinal_zero(string label)
    {
        var guide = Guide(Slot(label, ["Maximum Life"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Ring, 0));
    }

    [Fact]
    public void GoalBuildFactory_maps_generic_weapon_to_mainhand_role()
    {
        var guide = Guide(Slot("Weapon", ["Strength", "Critical Strike Damage"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(2));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand));
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void GoalBuildFactory_maps_bludgeoning_and_slicing_headers_to_barb_roles()
    {
        var guide = Guide(
            Slot("Bludgeoning", ["Strength"]),
            Slot("Slicing", ["Dexterity"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1), PlayerClass.Barbarian);

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Bludgeoning));
        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Slicing));
    }

    [Fact]
    public void GoalBuildFactory_maps_concrete_two_hand_header_to_twohand_role()
    {
        // A concrete 2H header classified for a non-Barbarian class lands on the TwoHand role.
        var guide = Guide(Slot("Two-Handed Sword", ["Strength"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.TwoHand));
        result.Warnings.ShouldNotContain(w => w.Contains("Unrecognized slot"));
    }

    [Fact]
    public void GoalBuildFactory_barbarian_concrete_two_hand_sword_header_maps_to_slicing_role()
    {
        // Same "Two-Handed Sword" header as above, but for Barbarian: MapSlot's concrete-header fallback
        // classifies through RoleForItemType, which routes Barbarian 2H swords to Slicing (not TwoHand) —
        // the same key an explicit "Slicing" label would produce.
        var guide = Guide(Slot("Two-Handed Sword", ["Strength"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1), PlayerClass.Barbarian);

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Slicing));
        result.GoalBuild.Goals.ShouldNotContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.TwoHand));
    }

    [Fact]
    public void GoalBuildFactory_barbarian_concrete_two_hand_mace_header_maps_to_bludgeoning_role()
    {
        var guide = Guide(Slot("Two-Handed Mace", ["Strength"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1), PlayerClass.Barbarian);

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Bludgeoning));
    }

    [Fact]
    public void GoalBuildFactory_druid_polearm_header_maps_to_twohand_not_slicing()
    {
        // Polearm is a two-handed irregular; for a non-Barbarian class it must land on TwoHand — the
        // Bludgeoning/Slicing split only applies when cls == Barbarian.
        var guide = Guide(Slot("Polearm", ["Strength"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1), PlayerClass.Druid);

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.TwoHand));
        result.GoalBuild.Goals.ShouldNotContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Slicing));
    }

    [Fact]
    public void GoalBuildFactory_rogue_bow_header_maps_to_twohand_role()
    {
        var guide = Guide(Slot("Bow", ["Dexterity"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1), PlayerClass.Rogue);

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.TwoHand));
    }

    [Fact]
    public void GoalBuildFactory_maps_offhand_type_to_offhand_role()
    {
        var guide = Guide(Slot("Focus", ["Intelligence"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand));
    }

    [Fact]
    public void GoalBuildFactory_maps_totem_offhand_type_to_offhand_role()
    {
        // Symmetric coverage to Focus above — Totem is another offhand-internal type.
        var guide = Guide(Slot("Totem", ["Intelligence"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand));
    }

    [Fact]
    public void GoalBuildFactory_bare_shield_header_maps_to_offhand_role()
    {
        // Shield lives in the Armor category but plays an offhand role, so the role map now classifies
        // it as Offhand rather than dropping it as an unrecognized slot.
        var guide = Guide(Slot("Shield", ["Maximum Life"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand));
    }

    [Fact]
    public void GoalBuildFactory_lowercase_concrete_weapon_header_fails_to_resolve()
    {
        // A web-scraped build guide may render the header in lowercase. NameResolver's exact lookup is
        // case-sensitive (StringComparison.Ordinal) and its fuzzy fallback is ambiguous here — "sword"
        // substring-matches BOTH "Sword" and "Two-Handed Sword", so TryFuzzyResolve refuses (2 matches)
        // and the header is left unrecognized even though "Two-Handed Sword" is a valid catalog type.
        var guide = Guide(Slot("two-handed sword", ["Strength"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldBeEmpty();
        result.Warnings.ShouldContain(w => w.Contains("Unrecognized slot") && w.Contains("two-handed sword"));
    }

    [Fact]
    public void GoalBuildFactory_warns_and_skips_unrecognized_slot_label()
    {
        var guide = Guide(Slot("Bracers", ["Maximum Life"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldBeEmpty();
        result.Warnings.ShouldContain(w => w.Contains("Unrecognized slot") && w.Contains("Bracers"));
    }

    [Fact]
    public void GoalBuildFactory_empty_guide_produces_empty_goal_build_with_no_warnings()
    {
        var guide = Guide();

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(3));

        result.GoalBuild.Goals.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void GoalBuildFactory_slot_with_no_resolvable_targets_is_omitted_with_warning()
    {
        var guide = Guide(Slot("Helm", []));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(3));

        result.GoalBuild.Goals.ShouldBeEmpty();
        result.Warnings.ShouldContain(w => w.Contains("No targets resolved") && w.Contains("Helm"));
    }

    [Fact]
    public void GoalBuildFactory_duplicate_slot_label_last_one_wins()
    {
        var guide = Guide(
            Slot("Helm", ["Critical Strike Chance"]),
            Slot("Helm", ["Maximum Life", "Cooldown Reduction"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(3));

        result.GoalBuild.Goals.Count.ShouldBe(1);
        result.GoalBuild.Goals[new SlotKey(GearSlot.Helm)].TargetAffixIds.Count.ShouldBe(2);
    }

    [Fact]
    public void GoalBuildFactory_resolves_unique_permissively_without_sentinel()
    {
        var guide = Guide(Slot("Helm", [], itemName: "Harlequin Crest", hasUnique: false));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(3));

        result.GoalBuild.Goals[new SlotKey(GearSlot.Helm)].TargetUnique.ShouldNotBeNull();
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void GoalBuildFactory_unresolvable_item_name_without_sentinel_is_silently_ignored()
    {
        var guide = Guide(Slot("Helm", ["Maximum Life"],
            itemName: "Not A Real Unique Zzqqxx", hasUnique: false));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals[new SlotKey(GearSlot.Helm)].TargetUnique.ShouldBeNull();
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void GoalBuildFactory_orders_resolved_affixes_by_explicit_priority_positional_last()
    {
        var data = new FilterDataService();
        var resolver = new NameResolver(data);
        resolver.TryResolveAffix("Maximum Life", out var lifeHash, out _);
        resolver.TryResolveAffix("Critical Strike Chance", out var critHash, out _);
        resolver.TryResolveAffix("Cooldown Reduction", out var cdrHash, out _);

        var guide = Guide(SlotWithPriorities("Helm",
            ("Maximum Life", 2),
            ("Critical Strike Chance", 1),
            ("Cooldown Reduction", 0))); // positional (0) => sorts last

        var result = new GoalBuildFactory(resolver, new WeaponRoleMap(resolver)).Create(guide, MeetsGoalThreshold.NOf(3));

        result.GoalBuild.Goals[new SlotKey(GearSlot.Helm)].TargetAffixIds
            .ShouldBe([critHash, lifeHash, cdrHash]);
    }

    [Fact]
    public void GoalBuildFactory_preserves_stable_order_for_multiple_positional_affixes()
    {
        var data = new FilterDataService();
        var resolver = new NameResolver(data);
        resolver.TryResolveAffix("Critical Strike Chance", out var critHash, out _);
        resolver.TryResolveAffix("Maximum Life", out var lifeHash, out _);
        resolver.TryResolveAffix("Cooldown Reduction", out var cdrHash, out _);

        var guide = Guide(SlotWithPriorities("Helm",
            ("Critical Strike Chance", 0),
            ("Maximum Life", 0),
            ("Cooldown Reduction", 0)));

        var result = new GoalBuildFactory(resolver, new WeaponRoleMap(resolver)).Create(guide, MeetsGoalThreshold.NOf(3));

        result.GoalBuild.Goals[new SlotKey(GearSlot.Helm)].TargetAffixIds
            .ShouldBe([critHash, lifeHash, cdrHash]);
    }

    [Fact]
    public void GoalBuildFactory_negative_priority_sorts_before_explicit_priorities()
    {
        var data = new FilterDataService();
        var resolver = new NameResolver(data);
        resolver.TryResolveAffix("Maximum Life", out var lifeHash, out _);
        resolver.TryResolveAffix("Cooldown Reduction", out var cdrHash, out _);

        var guide = Guide(SlotWithPriorities("Helm",
            ("Maximum Life", 1),
            ("Cooldown Reduction", -5)));

        var result = new GoalBuildFactory(resolver, new WeaponRoleMap(resolver)).Create(guide, MeetsGoalThreshold.NOf(2));

        result.GoalBuild.Goals[new SlotKey(GearSlot.Helm)].TargetAffixIds
            .ShouldBe([cdrHash, lifeHash]);
    }
}

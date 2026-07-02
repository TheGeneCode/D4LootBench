namespace D4LootBench.Core.Tests.Progression;

using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
using D4LootBench.Core.Progression;
using Shouldly;

public sealed class GoalBuildFactoryTests
{
    private static GoalBuildFactory NewFactory() => new(new NameResolver(new FilterDataService()));

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
        var result = new GoalBuildFactory(resolver).Create(guide, MeetsGoalThreshold.NOf(3));

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
    public void GoalBuildFactory_maps_generic_weapon_to_family_key()
    {
        var guide = Guide(Slot("Weapon", ["Strength", "Critical Strike Damage"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(2));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Weapon));
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void GoalBuildFactory_maps_concrete_weapon_header_to_family_key()
    {
        var guide = Guide(Slot("Two-Handed Sword", ["Strength"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Weapon));
        result.Warnings.ShouldNotContain(w => w.Contains("Unrecognized slot"));
    }

    [Fact]
    public void GoalBuildFactory_maps_offhand_type_to_offhand_family()
    {
        var guide = Guide(Slot("Focus", ["Intelligence"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Offhand));
    }

    [Fact]
    public void GoalBuildFactory_maps_totem_offhand_type_to_offhand_family()
    {
        // Symmetric coverage to Focus above — Totem is the other name-substring exception in MapSlot.
        var guide = Guide(Slot("Totem", ["Intelligence"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldContainKey(new SlotKey(GearSlot.Offhand));
    }

    [Fact]
    public void GoalBuildFactory_bare_shield_header_is_unrecognized_due_to_armor_category()
    {
        // Known gap called out in the handoff: "Shield" resolves via NameResolver but its catalog
        // category is "Armor", not "Weapons", so MapSlot's category gate rejects it and the slot is
        // silently dropped with a warning rather than mapped to Offhand.
        var guide = Guide(Slot("Shield", ["Maximum Life"]));

        var result = NewFactory().Create(guide, MeetsGoalThreshold.NOf(1));

        result.GoalBuild.Goals.ShouldBeEmpty();
        result.Warnings.ShouldContain(w => w.Contains("Unrecognized slot") && w.Contains("Shield"));
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

        var result = new GoalBuildFactory(resolver).Create(guide, MeetsGoalThreshold.NOf(3));

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

        var result = new GoalBuildFactory(resolver).Create(guide, MeetsGoalThreshold.NOf(3));

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

        var result = new GoalBuildFactory(resolver).Create(guide, MeetsGoalThreshold.NOf(2));

        result.GoalBuild.Goals[new SlotKey(GearSlot.Helm)].TargetAffixIds
            .ShouldBe([cdrHash, lifeHash]);
    }
}

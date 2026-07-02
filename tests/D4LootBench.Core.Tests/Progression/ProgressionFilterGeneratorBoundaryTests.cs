namespace D4LootBench.Core.Tests.Progression;

using System.Collections.Concurrent;
using D4LootBench.Core.Codec;
using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Models;
using D4LootBench.Core.Progression;
using Shouldly;
using static D4LootBench.Core.Tests.Progression.ProgressionTestFactory;

/// <summary>Boundary-focused coverage for <see cref="ProgressionFilterGenerator"/> beyond the
/// baseline suite in <see cref="ProgressionFilterGeneratorTests"/>: exact rule-budget boundaries
/// (with and without a uniques rule shrinking capacity), affix-count clamping at the slot level,
/// zero-affix slots (both resolvable and ambiguous), unique de-dup/cap, and thread-safety of
/// repeated/concurrent <see cref="ProgressionFilterGenerator.Generate"/> calls on one instance.</summary>
public sealed class ProgressionFilterGeneratorBoundaryTests
{
    private const int NoUniqueCapacity = FilterRuleset.MaxRuleCount - 1;   // 24: Hide All only is mandatory
    private const int WithUniqueCapacity = FilterRuleset.MaxRuleCount - 2; // 23: Hide All + Uniques mandatory

    private static ProgressionFilterGenerator NewGenerator() =>
        new(new NameResolver(new FilterDataService()));

    private static SlotDiffResult Diff(params SlotDiff[] slots) => new() { Slots = slots };

    private static SlotDiff NeedsUniqueRule(GearSlot slot, int ordinal, uint targetUnique, params uint[] affixes) =>
        new()
        {
            Slot = new SlotKey(slot, ordinal),
            Status = SlotDiffStatus.NeedsRule,
            Goal = new SlotGoal { TargetUnique = targetUnique, TargetAffixIds = affixes },
            TargetAffixIds = affixes,
        };

    // ---- Budget boundary: exact slack == 0, no uniques rule ----

    [Fact]
    public void Generate_NeedyEqualsCapacity_NoUnique_NoStrictRulesNoDrops()
    {
        var slots = Enumerable.Range(0, NoUniqueCapacity)
            .Select(i => NeedsRule(GearSlot.Helm, i, 1u, 2u, 3u, 4u))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeFalse();
        result.Warnings.ShouldNotContain(w => w.Contains("dropped"));
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Show).ShouldBe(0); // no slack → no strict rules
        result.TotalRuleCount.ShouldBe(1 + NoUniqueCapacity);
        result.Ruleset.Rules.Count.ShouldBeLessThanOrEqualTo(FilterRuleset.MaxRuleCount);
    }

    [Fact]
    public void Generate_NeedyOneOverCapacity_NoUnique_DropsExactlyOne()
    {
        var slots = Enumerable.Range(0, NoUniqueCapacity + 1)
            .Select(i => NeedsRule(GearSlot.Helm, i, 1u, 2u, 3u, 4u))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeTrue();
        result.Warnings.Count(w => w.Contains("dropped")).ShouldBe(1);
        result.Ruleset.Rules.Count.ShouldBe(1 + NoUniqueCapacity);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Show).ShouldBe(0); // over budget → base rules only
    }

    // ---- Budget boundary: a uniques rule shrinks capacity by one ----

    [Fact]
    public void Generate_NeedyEqualsCapacity_WithUnique_NoStrictRulesNoDrops()
    {
        var slots = Enumerable.Range(0, WithUniqueCapacity)
            .Select(i => NeedsUniqueRule(GearSlot.Helm, i, 0xAAAAu, 1u, 2u, 3u, 4u))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeFalse();
        result.Ruleset.Rules.ShouldNotContain(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal));
        result.TotalRuleCount.ShouldBe(1 /* Hide All */ + 1 /* Uniques */ + WithUniqueCapacity);
        result.Ruleset.Rules.Count.ShouldBeLessThanOrEqualTo(FilterRuleset.MaxRuleCount);
    }

    [Fact]
    public void Generate_NeedyOneOverCapacity_WithUnique_DropsExactlyOne()
    {
        var slots = Enumerable.Range(0, WithUniqueCapacity + 1)
            .Select(i => NeedsUniqueRule(GearSlot.Helm, i, 0xAAAAu, 1u, 2u, 3u, 4u))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeTrue();
        result.Warnings.Count(w => w.Contains("dropped")).ShouldBe(1);
        result.Ruleset.Rules.Count.ShouldBe(1 + 1 + WithUniqueCapacity);
    }

    // ---- Affix-count clamping on generated slot rules ----

    [Fact]
    public void Generate_SingleAffixSlot_StrictRequiredClampsToAffixCount()
    {
        var diff = Diff(NeedsRule(GearSlot.Helm, 0, 1u)); // only one target affix

        var result = NewGenerator().Generate(diff);

        var baseRule = result.Ruleset.Rules.Single(r => r.Name == "Helm");
        baseRule.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);

        var strictRule = result.Ruleset.Rules.Single(r => r.Name == "Helm (Greater)");
        strictRule.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
        strictRule.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_MoreThanFourTargetAffixes_TruncatesToFourPerRule()
    {
        var diff = Diff(NeedsRule(GearSlot.Helm, 0, 1u, 2u, 3u, 4u, 5u, 6u));

        var result = NewGenerator().Generate(diff);

        var baseRule = result.Ruleset.Rules.Single(r => r.Name == "Helm");
        baseRule.Conditions.OfType<AffixCondition>().Single().AffixIds
            .ShouldBe(new uint[] { 1u, 2u, 3u, 4u });
    }

    [Fact]
    public void Generate_ZeroAffixResolvableSlot_ItemTypeOnlyRuleNoCrash()
    {
        // Helm resolves to a real item type; zero target affixes → item-type-only rule, no AffixCondition.
        var diff = Diff(NeedsRule(GearSlot.Helm, 0));

        var result = NewGenerator().Generate(diff);

        var baseRule = result.Ruleset.Rules.Single(r => r.Name == "Helm");
        baseRule.Conditions.OfType<ItemTypeCondition>().ShouldHaveSingleItem();
        baseRule.Conditions.OfType<AffixCondition>().ShouldBeEmpty();
        result.Warnings.ShouldNotContain(w => w.Contains("No conditions"));
    }

    [Fact]
    public void Generate_ZeroAffixResolvableSlot_StrictRuleRoundTripsThroughCodec()
    {
        // Zero affixes + slack available → strict rule ends up as ItemTypeCondition + GreaterAffixCondition
        // only (no AffixCondition at all) — an unusual shape worth a dedicated round-trip check.
        var diff = Diff(NeedsRule(GearSlot.Helm, 0));

        var result = NewGenerator().Generate(diff);

        var strictRule = result.Ruleset.Rules.Single(r => r.Name == "Helm (Greater)");
        strictRule.Conditions.OfType<AffixCondition>().ShouldBeEmpty();
        strictRule.Conditions.OfType<GreaterAffixCondition>().ShouldHaveSingleItem();

        var decoded = FilterCodec.Decode(FilterCodec.Encode(result.Ruleset));
        var decodedStrict = decoded.Rules.Single(r => r.Name == "Helm (Greater)");
        decodedStrict.Conditions.Select(c => c.GetType().Name)
            .ShouldBe(strictRule.Conditions.Select(c => c.GetType().Name));
    }

    [Fact]
    public void Generate_ZeroAffixAmbiguousSlot_SkippedWithNoConditionsWarning()
    {
        // Weapon is ambiguous (no item-type gate) and has zero target affixes →
        // SlotRuleBuilder.Build returns null → the slot is skipped entirely, no crash.
        var diff = Diff(NeedsRule(GearSlot.Weapon, 0));

        var result = NewGenerator().Generate(diff);

        result.Ruleset.Rules.ShouldNotContain(r => r.Name == "Weapon");
        result.SlotRuleCount.ShouldBe(0);
        result.Warnings.ShouldContain(w => w.Contains("No conditions") && w.Contains("Weapon"));
        result.Ruleset.Rules.ShouldHaveSingleItem(); // Hide All only
    }

    [Fact]
    public void Generate_UnknownSlot_TreatedAsAmbiguous()
    {
        var diff = Diff(NeedsRule(GearSlot.Unknown, 0, 1u, 2u));

        var result = NewGenerator().Generate(diff);

        var rule = result.Ruleset.Rules.Single(r => r.Name == "Unknown");
        rule.Conditions.OfType<ItemTypeCondition>().ShouldBeEmpty();
        result.Warnings.ShouldContain(w => w.Contains("Unknown") && w.Contains("Ambiguous"));
    }

    [Fact]
    public void Generate_OffhandSlot_TreatedAsAmbiguous()
    {
        var diff = Diff(NeedsRule(GearSlot.Offhand, 0, 1u, 2u));

        var result = NewGenerator().Generate(diff);

        var rule = result.Ruleset.Rules.Single(r => r.Name == "Offhand");
        rule.Conditions.OfType<ItemTypeCondition>().ShouldBeEmpty();
        result.Warnings.ShouldContain(w => w.Contains("Offhand") && w.Contains("Ambiguous"));
    }

    // ---- Uniques: cap boundary + de-dup ----

    [Fact]
    public void Generate_ExactlyTenTargetUniques_NoWarningAllKept()
    {
        var slots = Enumerable.Range(0, SpecificUniqueCondition.MaxSelectionCount)
            .Select(i => NeedsUniqueRule(GearSlot.Amulet, i, (uint)(0x1000 + i), 1u))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        var uniqueRule = result.Ruleset.Rules.Single(r => r.Name == "Target Uniques");
        uniqueRule.Conditions.OfType<SpecificUniqueCondition>().Single().UniqueIds.Count
            .ShouldBe(SpecificUniqueCondition.MaxSelectionCount);
        result.Warnings.ShouldNotContain(w => w.Contains("target uniques"));
    }

    [Fact]
    public void Generate_ElevenTargetUniques_CapsAtTenWithWarning()
    {
        var slots = Enumerable.Range(0, SpecificUniqueCondition.MaxSelectionCount + 1)
            .Select(i => NeedsUniqueRule(GearSlot.Amulet, i, (uint)(0x1000 + i), 1u))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        var uniqueRule = result.Ruleset.Rules.Single(r => r.Name == "Target Uniques");
        uniqueRule.Conditions.OfType<SpecificUniqueCondition>().Single().UniqueIds.Count
            .ShouldBe(SpecificUniqueCondition.MaxSelectionCount);
        result.Warnings.ShouldContain(w => w.Contains("More than 10 target uniques"));
    }

    [Fact]
    public void Generate_DuplicateTargetUniqueAcrossSlots_DeDuplicated()
    {
        var diff = Diff(
            NeedsUniqueRule(GearSlot.Amulet, 0, 0xBEEFu, 1u),
            NeedsUniqueRule(GearSlot.Ring, 0, 0xBEEFu, 2u));

        var result = NewGenerator().Generate(diff);

        var uniqueRule = result.Ruleset.Rules.Single(r => r.Name == "Target Uniques");
        uniqueRule.Conditions.OfType<SpecificUniqueCondition>().Single()
            .UniqueIds.ShouldBe([0xBEEFu]);
    }

    // ---- All-slots-meet-goal via the real diff engine (not a directly-constructed empty list) ----

    [Fact]
    public void Generate_AllSlotsMeetGoalViaRealEngine_OnlyHideAll()
    {
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Helm)] = new() { TargetAffixIds = [1u, 2u] },
        });
        var loadout = EquippedLoadout.FromItems([Item(GearSlot.Helm, [Affix(1u), Affix(2u)])]);
        var diff = new SlotDiffEngine().Diff(loadout, goal);

        var result = NewGenerator().Generate(diff);

        result.Ruleset.Rules.ShouldHaveSingleItem();
        result.Ruleset.Rules[0].Visibility.ShouldBe(Visibility.HideAll);
        result.Warnings.ShouldBeEmpty();
    }

    // ---- State isolation: repeated and concurrent calls on one generator instance ----

    [Fact]
    public void Generate_RepeatedCallsSameInstance_ProduceIndependentResults()
    {
        var generator = NewGenerator();

        var first = generator.Generate(Diff(NeedsRule(GearSlot.Helm, 0, 1u, 2u)));
        var second = generator.Generate(Diff()); // empty diff, called second on the same instance

        first.Ruleset.Rules.ShouldContain(r => r.Name == "Helm");
        second.Ruleset.Rules.ShouldHaveSingleItem(); // Hide All only — unaffected by the prior call
        second.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Generate_ConcurrentCallsSameInstance_EachResultIndependentAndCorrect()
    {
        var generator = NewGenerator();
        var results = new ConcurrentBag<(int Index, ProgressionFilterResult Result)>();

        Parallel.For(0, 50, i =>
        {
            var diff = Diff(NeedsRule(GearSlot.Helm, 0, (uint)i, (uint)(i + 1)));
            var result = generator.Generate(diff, $"Filter {i}");
            results.Add((i, result));
        });

        results.Count.ShouldBe(50);
        foreach (var (i, result) in results)
        {
            result.Ruleset.Name.ShouldBe($"Filter {i}");
            var affixCondition = result.Ruleset.Rules.Single(r => r.Name == "Helm")
                .Conditions.OfType<AffixCondition>().Single();
            affixCondition.AffixIds.ShouldBe(new uint[] { (uint)i, (uint)(i + 1) });
        }
    }
}

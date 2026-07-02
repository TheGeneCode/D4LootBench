namespace D4LootBench.Core.Tests.Progression;

using System.Threading.Tasks;
using D4LootBench.Core.Codec;
using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Models;
using D4LootBench.Core.Progression;
using Shouldly;
using static D4LootBench.Core.Tests.Progression.ProgressionTestFactory;
using static VerifyXunit.Verifier;

public sealed class ProgressionFilterGeneratorTests
{
    private static ProgressionFilterGenerator NewGenerator() =>
        new(new NameResolver(new FilterDataService()));

    private static SlotDiffResult Diff(params SlotDiff[] slots) => new() { Slots = slots };

    private static FilterRule? RuleNamed(ProgressionFilterResult result, string name) =>
        result.Ruleset.Rules.FirstOrDefault(r => r.Name == name);

    [Fact]
    public void Generate_CompletedSlot_EmitsNoRule()
    {
        // Helm meets a 3-of-M goal; Gloves has only one of its targets → needs a rule.
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Helm)] = new() { TargetAffixIds = [1u, 2u, 3u] },
            [new SlotKey(GearSlot.Gloves)] = new() { TargetAffixIds = [4u, 5u, 6u] },
        });

        var loadout = EquippedLoadout.FromItems(
        [
            Item(GearSlot.Helm, [Affix(1u), Affix(2u), Affix(3u)]),
            Item(GearSlot.Gloves, [Affix(4u)]),
        ]);

        var diff = new SlotDiffEngine().Diff(loadout, goal);
        var result = NewGenerator().Generate(diff);

        RuleNamed(result, "Helm").ShouldBeNull();
        RuleNamed(result, "Gloves").ShouldNotBeNull();
        result.SlotRuleCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Generate_FewNeedySlots_SpendsBudgetOnStrictRules()
    {
        var diff = Diff(
            NeedsRule(GearSlot.Helm, 0, 1u, 2u, 3u, 4u),
            NeedsRule(GearSlot.Gloves, 0, 1u, 2u, 3u, 4u),
            NeedsRule(GearSlot.Boots, 0, 1u, 2u, 3u, 4u));

        var result = NewGenerator().Generate(diff);

        // Each needy slot gets a base (Recolor) + strict (Show) rule; plus Hide All.
        result.TotalRuleCount.ShouldBe(1 + (3 * 2));
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Recolor).ShouldBe(3);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Show).ShouldBe(3);
    }

    [Fact]
    public void Generate_StrictRule_TightensThreshold()
    {
        var diff = Diff(NeedsRule(GearSlot.Helm, 0, 1u, 2u, 3u, 4u));

        var result = NewGenerator().Generate(diff);

        var baseRule = RuleNamed(result, "Helm")!;
        var baseAffix = baseRule.Conditions.OfType<AffixCondition>().Single();
        baseAffix.MinimumCount.ShouldBe(2);
        baseRule.Conditions.OfType<GreaterAffixCondition>().ShouldBeEmpty();

        var strictRule = RuleNamed(result, "Helm (Greater)")!;
        strictRule.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(3);
        strictRule.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_PartialSlack_LimitsStrictRules()
    {
        // 22 needy slots: capacity 24, slack 2 → exactly 2 strict rules (first two slots).
        var slots = Enumerable.Range(0, 22)
            .Select(i => NeedsRule(GearSlot.Helm, i, 1u, 2u, 3u, 4u))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeFalse();
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Show).ShouldBe(2);
        result.TotalRuleCount.ShouldBe(1 + 22 + 2);

        // base-then-strict grouped per slot; only the first two slots get a strict rule.
        result.Ruleset.Rules[0].Name.ShouldBe("Helm");
        result.Ruleset.Rules[1].Name.ShouldBe("Helm (Greater)");
        result.Ruleset.Rules[2].Name.ShouldBe("Helm#2");
        result.Ruleset.Rules[3].Name.ShouldBe("Helm#2 (Greater)");
        result.Ruleset.Rules[4].Name.ShouldBe("Helm#3"); // third slot: base only
    }

    [Fact]
    public void Generate_OverBudget_CapsAt25AndWarns()
    {
        var slots = Enumerable.Range(0, 30)
            .Select(i => NeedsRule(GearSlot.Helm, i, 1u, 2u, 3u, 4u))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeTrue();
        result.Ruleset.Rules.Count.ShouldBeLessThanOrEqualTo(25);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Show).ShouldBe(0); // base rules only
        result.Warnings.Count(w => w.Contains("dropped")).ShouldBe(30 - 24);
    }

    [Fact]
    public void Generate_TargetUnique_EmitsSpecificUniqueRule()
    {
        var diff = Diff(new SlotDiff
        {
            Slot = new SlotKey(GearSlot.Amulet),
            Status = SlotDiffStatus.NeedsRule,
            Goal = new SlotGoal { TargetUnique = 0xABCu, TargetAffixIds = [1u, 2u] },
            TargetAffixIds = [1u, 2u],
        });

        var result = NewGenerator().Generate(diff);

        var uniqueRule = RuleNamed(result, "Target Uniques")!;
        uniqueRule.Conditions.OfType<SpecificUniqueCondition>().Single()
            .UniqueIds.ShouldContain(0xABCu);
    }

    [Fact]
    public void Generate_AmbiguousSlot_AffixOnlyRuleWithWarning()
    {
        var diff = Diff(NeedsRule(GearSlot.Weapon, 0, 1u, 2u, 3u));

        var result = NewGenerator().Generate(diff);

        var rule = RuleNamed(result, "Weapon")!;
        rule.Conditions.OfType<AffixCondition>().ShouldHaveSingleItem();
        rule.Conditions.OfType<ItemTypeCondition>().ShouldBeEmpty();
        result.Warnings.ShouldContain(w => w.Contains("Weapon") && w.Contains("Ambiguous"));
    }

    [Fact]
    public void Generate_NoNeedySlots_OnlyHideAll()
    {
        var result = NewGenerator().Generate(Diff());

        result.Ruleset.Rules.ShouldHaveSingleItem();
        result.Ruleset.Rules[0].Visibility.ShouldBe(Visibility.HideAll);
        result.SlotRuleCount.ShouldBe(0);
    }

    [Fact]
    public void Generate_RoundTripsThroughCodec()
    {
        var result = NewGenerator().Generate(GoldenDiff());

        var decoded = FilterCodec.Decode(FilterCodec.Encode(result.Ruleset));

        decoded.Rules.Count.ShouldBe(result.Ruleset.Rules.Count);
        for (var i = 0; i < decoded.Rules.Count; i++)
        {
            decoded.Rules[i].Conditions.Select(c => c.GetType().Name)
                .ShouldBe(result.Ruleset.Rules[i].Conditions.Select(c => c.GetType().Name));
        }
    }

    [Fact]
    public Task Generate_Golden_ShareCodeSnapshot()
    {
        var result = NewGenerator().Generate(GoldenDiff());
        return Verify(FilterCodec.Encode(result.Ruleset));
    }

    [Fact]
    public Task Generate_Golden_StructureSnapshot()
    {
        var result = NewGenerator().Generate(GoldenDiff());
        return Verify(result.Ruleset);
    }

    // Fixed 3-needy (real item types) + 1 target unique — deterministic snapshot input.
    private static SlotDiffResult GoldenDiff() => new()
    {
        Slots =
        [
            new SlotDiff
            {
                Slot = new SlotKey(GearSlot.Helm),
                Status = SlotDiffStatus.NeedsRule,
                Goal = new SlotGoal { TargetUnique = 0xFEEDu, TargetAffixIds = [0x11u, 0x22u, 0x33u, 0x44u] },
                TargetAffixIds = [0x11u, 0x22u, 0x33u, 0x44u],
            },
            new SlotDiff
            {
                Slot = new SlotKey(GearSlot.Gloves),
                Status = SlotDiffStatus.NeedsRule,
                TargetAffixIds = [0x55u, 0x66u, 0x77u],
            },
            new SlotDiff
            {
                Slot = new SlotKey(GearSlot.Boots),
                Status = SlotDiffStatus.NeedsRule,
                TargetAffixIds = [0x88u, 0x99u],
            },
        ],
    };
}

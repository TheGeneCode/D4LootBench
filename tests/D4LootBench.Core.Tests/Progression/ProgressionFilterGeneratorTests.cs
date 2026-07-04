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
    private static ProgressionFilterGenerator NewGenerator()
    {
        var resolver = new NameResolver(new FilterDataService());
        return new(resolver, new WeaponRoleMap(resolver));
    }

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
    public void Generate_EachNeedySlot_EmitsExactlyOneRecolorRule()
    {
        var diff = Diff(
            NeedsRule(GearSlot.Helm, 0, 1u, 2u, 3u, 4u),
            NeedsRule(GearSlot.Gloves, 0, 1u, 2u, 3u, 4u),
            NeedsRule(GearSlot.Boots, 0, 1u, 2u, 3u, 4u));

        var result = NewGenerator().Generate(diff);

        // One Recolor rule per slot + Hide All; no "way better" (Greater) rules at all.
        result.TotalRuleCount.ShouldBe(3 + 1);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Recolor).ShouldBe(3);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Show).ShouldBe(0);
        result.Ruleset.Rules.ShouldNotContain(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_NonMaxedSlot_EmitsPinkSameOrMoreRule()
    {
        // Equipped item has 2 of 4 targets and is not yet maxed → a single pink rule highlighting items
        // with the SAME OR MORE (2+) target affixes. No GreaterAffixCondition and no (Greater) rule.
        var diff = Diff(new SlotDiff
        {
            Slot = new SlotKey(GearSlot.Helm),
            Status = SlotDiffStatus.NeedsRule,
            TargetAffixIds = [1u, 2u, 3u, 4u],
            MatchedAffixCount = 2,
            EffectiveTargetCap = 4,
            IsMaxedOnTargets = false,
        });

        var result = NewGenerator().Generate(diff);

        var rule = RuleNamed(result, "Helm")!;
        rule.Visibility.ShouldBe(Visibility.Recolor);
        rule.Color.ShouldBe(FilterColors.LightPurple);
        rule.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(2);
        rule.Conditions.OfType<GreaterAffixCondition>().ShouldBeEmpty();
        RuleNamed(result, "Helm (Greater)").ShouldBeNull();
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Recolor).ShouldBe(1);
    }

    [Fact]
    public void Generate_MaxedZeroGa_EmitsCyanGaRule()
    {
        // Slot maxed on 3 of 3 targets, none greater → a single cyan (Greater) rule: the same 3 affixes
        // plus at least one Greater Affix. No pink rule is emitted for a maxed slot.
        var diff = Diff(MaxedNeedsRule(GearSlot.Ring, 3, 0, 3, 1u, 2u, 3u));

        var result = NewGenerator().Generate(diff);

        var cyan = RuleNamed(result, "Ring (Greater)")!;
        cyan.Visibility.ShouldBe(Visibility.Recolor);
        cyan.Color.ShouldBe(FilterRule.PackColor(0, 220, 255));
        cyan.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(3);
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
        RuleNamed(result, "Ring").ShouldBeNull();
    }

    [Fact]
    public void Generate_MaxedTwoGa_RequiresSameOrMoreGa()
    {
        // Maxed on 3 of 3 with 2 already greater → cyan rule demands at least 2 GA (same-or-more) at the
        // same target-affix count.
        var diff = Diff(MaxedNeedsRule(GearSlot.Ring, 3, 2, 3, 1u, 2u, 3u));

        var result = NewGenerator().Generate(diff);

        var cyan = RuleNamed(result, "Ring (Greater)")!;
        cyan.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(3);
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(2);
    }

    [Fact]
    public void Generate_PinkRanksAboveCyan()
    {
        // A mix of non-maxed (pink) and maxed (cyan) needy slots: every pink rule precedes every cyan rule
        // so the lower-value maxed-GA rules are the first to drop under the cap.
        var diff = Diff(
            new SlotDiff
            {
                Slot = new SlotKey(GearSlot.Helm),
                Status = SlotDiffStatus.NeedsRule,
                TargetAffixIds = [1u, 2u],
                MatchedAffixCount = 1,
                EffectiveTargetCap = 2,
            },
            MaxedNeedsRule(GearSlot.Ring, 2, 0, 2, 3u, 4u));

        var result = NewGenerator().Generate(diff);

        var slotRules = result.Ruleset.Rules.Where(r => r.Visibility == Visibility.Recolor).ToList();
        var lastPink = slotRules.FindLastIndex(r => r.Color == FilterColors.LightPurple);
        var firstCyan = slotRules.FindIndex(r => r.Color == FilterRule.PackColor(0, 220, 255));
        lastPink.ShouldBeLessThan(firstCyan);
    }

    [Fact]
    public void Generate_OverCap_DropsCyanFirst()
    {
        // 20 distinct pink (non-maxed) + 20 distinct cyan (maxed) exceed the 24-rule capacity; pink ranks
        // first, so all 20 pink survive and every dropped rule is a cyan (Greater) one.
        var pink = Enumerable.Range(0, 20)
            .Select(i => NeedsRule(GearSlot.Helm, i, (uint)(1000 + i), 2u))
            .ToArray();
        var cyan = Enumerable.Range(0, 20)
            .Select(i => MaxedNeedsRule(GearSlot.Ring, 1, 0, 1, (uint)(2000 + i)))
            .ToArray();

        var result = NewGenerator().Generate(Diff(pink.Concat(cyan).ToArray()));

        result.BudgetExceeded.ShouldBeTrue();
        result.Warnings.Where(w => w.Contains("dropped")).ShouldAllBe(w => w.Contains("(Greater)"));
        result.Ruleset.Rules.Count(r => r.Color == FilterColors.LightPurple).ShouldBe(20);
    }

    [Fact]
    public void Generate_EmptySlot_RequiresAtLeastOneTargetAffix()
    {
        // No equipped gear (MatchedAffixCount defaults to 0) → require 1, so any target affix highlights.
        var diff = Diff(NeedsRule(GearSlot.Helm, 0, 1u, 2u, 3u));

        var result = NewGenerator().Generate(diff);

        RuleNamed(result, "Helm")!.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_RoleSlotName_StaysWithinDisplayLimit()
    {
        // Role names drive the rule name (e.g. "Bludgeoning"); the generator caps to the display limit
        // so D4 never blanks an over-long name (→ "Rule #N").
        var diff = Diff(NeedsRule(GearSlot.Weapon, WeaponSlotRole.Bludgeoning, 1u, 2u));

        var result = NewGenerator().Generate(diff, PlayerClass.Barbarian);

        var rule = result.Ruleset.Rules.Single(r => r.Visibility == Visibility.Recolor);
        rule.Name.Length.ShouldBeLessThanOrEqualTo(SlotRuleBuilder.MaxNameLength);
        rule.Name.ShouldBe("Bludgeoning");
    }

    [Fact]
    public void Generate_OverBudget_CapsAt25AndWarns()
    {
        // Distinct affix sets per slot so the same-shape collapse doesn't merge them before the cap.
        var slots = Enumerable.Range(0, 30)
            .Select(i => NeedsRule(GearSlot.Helm, i, (uint)(1000 + i), 2u, 3u, 4u))
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
    public void ProgressionFilter_barb_slicing_gates_multi_type()
    {
        var resolver = new NameResolver(new FilterDataService());
        uint TypeHash(string n)
        {
            resolver.TryResolveItemType(n, out var h, out _).ShouldBeTrue();
            return h;
        }

        var result = new ProgressionFilterGenerator(resolver, new WeaponRoleMap(resolver))
            .Generate(Diff(NeedsRule(GearSlot.Weapon, WeaponSlotRole.Slicing, 1u, 2u, 3u)), PlayerClass.Barbarian);

        var rule = result.Ruleset.Rules.Single(r => r.Conditions.OfType<ItemTypeCondition>().Any());
        var typeIds = rule.Conditions.OfType<ItemTypeCondition>().Single().TypeIds;
        typeIds.ShouldBe(new[] { TypeHash("Polearm"), TypeHash("Two-Handed Sword"), TypeHash("Two-Handed Axe") }
            .OrderBy(h => h).ToList());
        result.Warnings.ShouldNotContain(w => w.Contains("Ambiguous item type"));

        // Codec round-trip is the format canary.
        var decoded = FilterCodec.Decode(FilterCodec.Encode(result.Ruleset));
        decoded.Rules.Count.ShouldBe(result.Ruleset.Rules.Count);
    }

    [Fact]
    public void ProgressionFilter_warns_when_weapon_role_absent()
    {
        // A weapon slot with no role (OCR type unresolved) has no item-type gate → affix-only + warning.
        var result = NewGenerator().Generate(Diff(NeedsRule(GearSlot.Weapon, 0, 1u, 2u, 3u)));

        var rule = RuleNamed(result, "Weapon")!;
        rule.Conditions.OfType<AffixCondition>().ShouldHaveSingleItem();
        rule.Conditions.OfType<ItemTypeCondition>().ShouldBeEmpty();
        result.Warnings.ShouldContain(w => w.Contains("Ambiguous item type"));
    }

    [Fact]
    public void ProgressionFilter_barb_hands_same_affixes_collapse()
    {
        // Mainhand + Offhand with identical targets/matched resolve to the same 1H type set → one rule.
        var diff = Diff(
            NeedsRule(GearSlot.Weapon, WeaponSlotRole.Mainhand, 1u, 2u),
            NeedsRule(GearSlot.Offhand, WeaponSlotRole.Offhand, 1u, 2u));

        var result = NewGenerator().Generate(diff, PlayerClass.Barbarian);

        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Recolor).ShouldBe(1);
        result.SlotRuleCount.ShouldBe(1);
    }

    [Fact]
    public void ProgressionFilter_barb_hands_diff_affixes_two_rules()
    {
        var diff = Diff(
            NeedsRule(GearSlot.Weapon, WeaponSlotRole.Mainhand, 1u, 2u),
            NeedsRule(GearSlot.Offhand, WeaponSlotRole.Offhand, 3u, 4u));

        var result = NewGenerator().Generate(diff, PlayerClass.Barbarian);

        var recolor = result.Ruleset.Rules.Where(r => r.Visibility == Visibility.Recolor).ToList();
        recolor.Count.ShouldBe(2);
        recolor.Select(r => r.Conditions.OfType<AffixCondition>().Single().AffixIds)
            .Select(ids => string.Join(",", ids)).Distinct().Count().ShouldBe(2);
    }

    // ---- Interchangeable slot pools: rings (all classes) and Barbarian 1H weapon hands compare to the
    // WORST equipped member of the pool instead of emitting one divergent rule per physical instance. ----

    [Fact]
    public void Generate_TwoRings_IdenticalGoal_WorstRing_OneRule()
    {
        // Two equipped rings sharing one goal (ordinal-stripped fallback): ordinal 0 matches only affix 1,
        // ordinal 1 matches affixes 1+2. The pool must key to the WORST ring (1 match) — a single "Ring" rule.
        var loadout = EquippedLoadout.FromItems(
            [Item(GearSlot.Ring, [Affix(1u)]), Item(GearSlot.Ring, [Affix(1u), Affix(2u)])],
            PlayerClass.Sorcerer, RoleMap());
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Ring)] = new() { TargetAffixIds = [1u, 2u, 3u] },
        });

        var diff = new SlotDiffEngine(MeetsGoalThreshold.RelativeToEquipped).Diff(loadout, goal);
        var result = NewGenerator().Generate(diff, PlayerClass.Sorcerer);

        var ringRules = result.Ruleset.Rules.Where(r => r.Name == "Ring").ToList();
        ringRules.ShouldHaveSingleItem();
        ringRules[0].Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_TwoRings_DistinctGoals_TwoRules_BothRingGated()
    {
        // Distinct per-ring goals (explicit per-ordinal, no fallback) still share the Ring item-type gate,
        // but split into two rules — one per distinct target-affix list — named "Ring" and "Ring 2".
        var loadout = EquippedLoadout.FromItems(
            [Item(GearSlot.Ring, [Affix(1u)]), Item(GearSlot.Ring, [Affix(3u)])],
            PlayerClass.Sorcerer, RoleMap());
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Ring, 0)] = new() { TargetAffixIds = [1u, 2u] },
            [new SlotKey(GearSlot.Ring, 1)] = new() { TargetAffixIds = [3u, 4u] },
        });

        var diff = new SlotDiffEngine(MeetsGoalThreshold.RelativeToEquipped).Diff(loadout, goal);
        var result = NewGenerator().Generate(diff, PlayerClass.Sorcerer);

        var first = RuleNamed(result, "Ring")!;
        var second = RuleNamed(result, "Ring 2")!;
        first.Conditions.OfType<ItemTypeCondition>().ShouldHaveSingleItem();
        second.Conditions.OfType<ItemTypeCondition>().ShouldHaveSingleItem();
        first.Conditions.OfType<AffixCondition>().Single().AffixIds.ShouldBe<IReadOnlyList<uint>>([1u, 2u]);
        second.Conditions.OfType<AffixCondition>().Single().AffixIds.ShouldBe<IReadOnlyList<uint>>([3u, 4u]);
    }

    [Fact]
    public void Generate_BarbHands_DifferentMatchCounts_OneRuleWorst()
    {
        // Barbarian dual 1H hands with the SAME target affixes but different matched counts (2 vs 1) pool
        // together (same class-aware 1H type set) and key to the WORST hand's matched count.
        var diff = Diff(
            new SlotDiff
            {
                Slot = new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand),
                Status = SlotDiffStatus.NeedsRule,
                TargetAffixIds = [1u, 2u],
                MatchedAffixCount = 2,
            },
            new SlotDiff
            {
                Slot = new SlotKey(GearSlot.Offhand, 0, WeaponSlotRole.Offhand),
                Status = SlotDiffStatus.NeedsRule,
                TargetAffixIds = [1u, 2u],
                MatchedAffixCount = 1,
            });

        var result = NewGenerator().Generate(diff, PlayerClass.Barbarian);

        var recolor = result.Ruleset.Rules.Where(r => r.Visibility == Visibility.Recolor).ToList();
        recolor.ShouldHaveSingleItem();
        result.SlotRuleCount.ShouldBe(1);
        recolor[0].Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_MaxedRingPair_CyanWorstGa()
    {
        // Two maxed rings sharing targets/cap but different GA counts (2 vs 0) pool into ONE cyan rule
        // keyed to the WORST (lowest) GA count.
        var diff = Diff(
            MaxedNeedsRule(GearSlot.Ring, 3, 2, 3, 1u, 2u, 3u),
            new SlotDiff
            {
                Slot = new SlotKey(GearSlot.Ring, 1),
                Status = SlotDiffStatus.NeedsRule,
                TargetAffixIds = [1u, 2u, 3u],
                MatchedAffixCount = 3,
                MatchedGreaterAffixCount = 0,
                EffectiveTargetCap = 3,
                IsMaxedOnTargets = true,
            });

        var result = NewGenerator().Generate(diff);

        result.Ruleset.Rules.Count(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(1);
        RuleNamed(result, "Ring (Greater)")!.Conditions.OfType<GreaterAffixCondition>().Single()
            .MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_SingleRing_UnchangedName()
    {
        // A single maxed ring (no pool partner) keeps today's exact name and values — guards the golden-
        // snapshot invariant that a singleton pool is byte-identical to pre-pooling output.
        var diff = Diff(MaxedNeedsRule(GearSlot.Ring, 3, 0, 3, 1u, 2u, 3u));

        var result = NewGenerator().Generate(diff);

        var cyan = RuleNamed(result, "Ring (Greater)")!;
        cyan.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(3);
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
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

    // Fixed 4-needy (real item types) + 1 target unique — deterministic snapshot input. Helm/Gloves/Boots
    // are non-maxed (matched 0) so they exercise the pink same-or-more rule (require 1 affix). The Ring slot
    // is MAXED on all 3 of its targets with NONE greater, so it exercises the cyan maxed-GA rule (require
    // the same 3 affixes + at least one Greater Affix).
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
            new SlotDiff
            {
                Slot = new SlotKey(GearSlot.Ring),
                Status = SlotDiffStatus.NeedsRule,
                EquippedItem = Item(GearSlot.Ring, [Affix(0xA1u), Affix(0xA2u), Affix(0xA3u)]),
                Goal = new SlotGoal { TargetAffixIds = [0xA1u, 0xA2u, 0xA3u] },
                TargetAffixIds = [0xA1u, 0xA2u, 0xA3u],
                MatchedAffixCount = 3,
                MatchedGreaterAffixCount = 0,
                EffectiveTargetCap = 3,
                IsMaxedOnTargets = true,
            },
        ],
    };
}

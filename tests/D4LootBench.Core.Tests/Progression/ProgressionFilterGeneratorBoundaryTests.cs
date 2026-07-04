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

    private static ProgressionFilterGenerator NewGenerator()
    {
        var resolver = new NameResolver(new FilterDataService());
        return new(resolver, new WeaponRoleMap(resolver));
    }

    private static SlotDiffResult Diff(params SlotDiff[] slots) => new() { Slots = slots };

    private static SlotDiff NeedsUniqueRule(GearSlot slot, int ordinal, uint targetUnique, params uint[] affixes) =>
        new()
        {
            Slot = new SlotKey(slot, ordinal),
            Status = SlotDiffStatus.NeedsRule,
            Goal = new SlotGoal { TargetUnique = targetUnique, TargetAffixIds = affixes },
            TargetAffixIds = affixes,
        };

    // A non-maxed NeedsRule diff with a real equipped item (IsMaxedOnTargets defaults false), used for the
    // pink same-or-more rule; matched/matchedGreater are set directly to probe the pink rule's boundaries.
    private static SlotDiff NeedsRuleEquipped(GearSlot slot, int ordinal, int matched, int matchedGreater, params uint[] targets) =>
        new()
        {
            Slot = new SlotKey(slot, ordinal),
            Status = SlotDiffStatus.NeedsRule,
            EquippedItem = Item(slot, []),
            Goal = new SlotGoal { TargetAffixIds = targets },
            TargetAffixIds = targets,
            MatchedAffixCount = matched,
            MatchedGreaterAffixCount = matchedGreater,
        };

    // A maxed-on-targets NeedsRule diff with an explicit ordinal (so cyan rules for distinct ordinals stay
    // separate for budget/ordering tests). IsMaxedOnTargets is derived from matched >= cap, matching the
    // engine, so the generator takes the cyan (Greater) branch.
    private static SlotDiff MaxedEquipped(GearSlot slot, int ordinal, int matched, int matchedGreater, int cap, params uint[] targets) =>
        new()
        {
            Slot = new SlotKey(slot, ordinal),
            Status = SlotDiffStatus.NeedsRule,
            EquippedItem = Item(slot, []),
            Goal = new SlotGoal { TargetAffixIds = targets },
            TargetAffixIds = targets,
            MatchedAffixCount = matched,
            MatchedGreaterAffixCount = matchedGreater,
            EffectiveTargetCap = cap,
            IsMaxedOnTargets = matched >= cap,
        };

    private static FilterRule? GreaterRule(ProgressionFilterResult result, string slotName) =>
        result.Ruleset.Rules.FirstOrDefault(r => r.Name == slotName + " (Greater)");

    // ---- Cyan maxed-slot rule: emission, shape, ordering, budget priority. Once the equipped piece is
    // maxed on its target affixes for its rarity the only catchable upgrade is more Greater Affixes, so the
    // slot emits a single cyan (Greater) rule instead of a pink one. ----

    [Fact]
    public void Generate_MaxedSlot_EmitsCyanGaRuleNoPink()
    {
        // Ring maxed on 3 of 3 targets, none greater → a single cyan rule requiring the same 3 affixes plus
        // at least one Greater Affix. No pink rule is emitted for a maxed slot.
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Ring, 0, 3, 0, 3, 1u, 2u, 3u)));

        var cyan = GreaterRule(result, "Ring").ShouldNotBeNull();
        cyan.Visibility.ShouldBe(Visibility.Recolor);
        cyan.Color.ShouldBe(FilterRule.PackColor(0, 220, 255));
        cyan.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(3);
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
        result.Ruleset.Rules.ShouldNotContain(r => r.Name == "Ring"); // no pink rule for a maxed slot
    }

    [Fact]
    public void Generate_MaxedSlot_RequiresSameOrMoreGa()
    {
        // Maxed on 3 of 3 with 2 already greater → cyan rule demands at least 2 GA (same-or-more) at the
        // same target-affix count.
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Amulet, 0, 3, 2, 3, 1u, 2u, 3u)));

        var cyan = GreaterRule(result, "Amulet").ShouldNotBeNull();
        cyan.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(3);
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(2);
    }

    [Fact]
    public void Generate_MaxedSingleTarget_MinimalCyan()
    {
        // Smallest maxed slot: one target, present but not greater (cap 1) → cyan requires that one affix
        // plus one GA — the minimal viable cyan rule.
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Ring, 0, 1, 0, 1, 1u)));

        var cyan = GreaterRule(result, "Ring").ShouldNotBeNull();
        cyan.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_MaxedSlot_MatchedGreaterFloorsGaAtOne()
    {
        // matchedGa 0 on a maxed 2-target slot → the GA requirement floors at 1 (Math.Max(1, 0)), never 0,
        // so the cyan rule always gates on at least one Greater Affix.
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Ring, 0, 2, 0, 2, 1u, 2u)));

        var cyan = GreaterRule(result, "Ring").ShouldNotBeNull();
        cyan.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(2);
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_NonMaxedSlot_NeverEmitsGreaterAffixCondition()
    {
        // A non-maxed equipped slot (matched 2 of 3, none greater) yields ONE pink rule requiring the SAME
        // OR MORE count (2, not 3) and no cyan rule — GreaterAffixCondition is exclusive to maxed slots.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Ring, 0, 2, 0, 1u, 2u, 3u)));

        var pink = result.Ruleset.Rules.Single(r => r.Name == "Ring");
        pink.Color.ShouldBe(FilterColors.LightPurple);
        pink.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(2); // same-or-more, not +1
        GreaterRule(result, "Ring").ShouldBeNull();
        result.Ruleset.Rules.ShouldNotContain(r => r.Conditions.OfType<GreaterAffixCondition>().Any());
    }

    [Fact]
    public void Generate_NonMaxedMatchedZero_PinkMinOneNoGa()
    {
        // Non-maxed with zero matched targets → pink rule floors the required count at 1, still no GA gate.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Ring, 0, 0, 0, 1u, 2u)));

        var pink = result.Ruleset.Rules.Single(r => r.Name == "Ring");
        pink.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
        GreaterRule(result, "Ring").ShouldBeNull();
    }

    [Fact]
    public void Generate_StaleDiff_MatchedGreaterExceedsMatched_NonMaxed_NoThrow()
    {
        // A hand-built/stale SlotDiff could report MORE greater matches than matches (shouldn't happen via
        // SlotDiffEngine, but Generate() takes a public SlotDiffResult). When not maxed the generator emits
        // a plain pink rule and must not throw on the out-of-range combination.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Ring, 0, 1, 5, 1u, 2u)));

        result.Ruleset.Rules.ShouldContain(r => r.Name == "Ring");
        GreaterRule(result, "Ring").ShouldBeNull();
    }

    [Fact]
    public void Generate_MaxedSlot_CapExceedsTargetCount_ClampsAffixMinNoThrow()
    {
        // A stale maxed diff whose EffectiveTargetCap exceeds the actual target count must not overflow the
        // cyan rule's AffixCondition — SlotRuleBuilder clamps requiredCount to [1, capped.Count].
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Helm, 0, 10, 0, 10, 1u, 2u, 3u)));

        var cyan = GreaterRule(result, "Helm").ShouldNotBeNull();
        var affixCondition = cyan.Conditions.OfType<AffixCondition>().Single();
        affixCondition.MinimumCount.ShouldBe(3);
        affixCondition.MinimumCount.ShouldBeLessThanOrEqualTo(affixCondition.AffixIds.Count);
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_MaxedSlot_RoundTripsThroughCodec()
    {
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Ring, 0, 2, 0, 2, 1u, 2u)));
        var cyan = GreaterRule(result, "Ring").ShouldNotBeNull();

        var decoded = FilterCodec.Decode(FilterCodec.Encode(result.Ruleset));
        var decodedCyan = decoded.Rules.Single(r => r.Name == "Ring (Greater)");
        decodedCyan.Conditions.Select(c => c.GetType().Name)
            .ShouldBe(cyan.Conditions.Select(c => c.GetType().Name));
        decodedCyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_MaxedAmbiguousWeapon_AffixOnlyCyanRoundTrips()
    {
        // Weapon with role None resolves to an EMPTY type-hash set (ambiguous), so the cyan rule has no
        // ItemTypeCondition at all — just AffixCondition + GreaterAffixCondition. Confirm this shape
        // survives encode/decode too.
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Weapon, 0, 2, 0, 2, 1u, 2u)));

        var cyan = GreaterRule(result, "Weapon").ShouldNotBeNull();
        cyan.Conditions.OfType<ItemTypeCondition>().ShouldBeEmpty();
        cyan.Conditions.OfType<AffixCondition>().ShouldHaveSingleItem();
        cyan.Conditions.OfType<GreaterAffixCondition>().ShouldHaveSingleItem();

        var decoded = FilterCodec.Decode(FilterCodec.Encode(result.Ruleset));
        var decodedCyan = decoded.Rules.Single(r => r.Name == "Weapon (Greater)");
        decodedCyan.Conditions.Select(c => c.GetType().Name)
            .ShouldBe(cyan.Conditions.Select(c => c.GetType().Name));
        decodedCyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_AllPinkRulesRankAboveAllCyanRules()
    {
        // A mix of non-maxed (pink) and maxed (cyan) slots: every pink rule must precede every cyan rule so
        // the lower-value cyan rules are the first to drop under the cap.
        var result = NewGenerator().Generate(Diff(
            NeedsRuleEquipped(GearSlot.Helm, 0, 1, 0, 1u, 2u),      // pink
            MaxedEquipped(GearSlot.Gloves, 0, 2, 0, 2, 3u, 4u)));   // cyan

        var names = result.Ruleset.Rules.Select(r => r.Name).ToList();
        names.IndexOf("Helm").ShouldBeLessThan(names.IndexOf("Gloves (Greater)"));
    }

    [Fact]
    public void Generate_BudgetPressure_DropsCyanBeforePink()
    {
        // 20 distinct non-maxed (pink) + 20 distinct maxed (cyan) = 40 rules for a 24-rule (no-unique)
        // capacity. Pink rules rank first, so all 20 pink survive and every dropped rule is a cyan one.
        var pink = Enumerable.Range(0, 20)
            .Select(i => NeedsRuleEquipped(GearSlot.Helm, i, 1, 0, (uint)(1000 + i)))
            .ToArray();
        var cyan = Enumerable.Range(0, 20)
            .Select(i => MaxedEquipped(GearSlot.Ring, i, 1, 0, 1, (uint)(2000 + i)))
            .ToArray();

        var result = NewGenerator().Generate(Diff(pink.Concat(cyan).ToArray()));

        result.BudgetExceeded.ShouldBeTrue();
        result.Warnings.Where(w => w.Contains("dropped")).ShouldAllBe(w => w.Contains("(Greater)"));
        result.Ruleset.Rules.Count(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal))
            .ShouldBe(NoUniqueCapacity - 20); // 4 cyan survive after the 20 pink rules
        result.Ruleset.Rules.Count(r => r.Color == FilterColors.LightPurple).ShouldBe(20);
    }

    [Fact]
    public void Generate_IdenticalCyanShapes_CollapseToOne()
    {
        // Two maxed Ring ordinals sharing item type, targets, cap AND matchedGa produce byte-identical cyan
        // rules (same ga:{n} shape key) — collapse must merge them just like pink rules.
        var result = NewGenerator().Generate(Diff(
            MaxedEquipped(GearSlot.Ring, 0, 2, 0, 2, 10u, 20u),
            MaxedEquipped(GearSlot.Ring, 1, 2, 0, 2, 10u, 20u)));

        result.Ruleset.Rules.Count(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public void Generate_DifferingCyanGa_PoolsToWorstGa()
    {
        // Two Ring ordinals sharing the same targets/cap but different matchedGa (1 vs 2) are an
        // interchangeable pool: they merge into ONE cyan rule keyed to the WORST (lowest) GA count, so an
        // upgrade to the weaker ring is never missed.
        var result = NewGenerator().Generate(Diff(
            MaxedEquipped(GearSlot.Ring, 0, 2, 1, 2, 10u, 20u),
            MaxedEquipped(GearSlot.Ring, 1, 2, 2, 2, 10u, 20u)));

        result.Ruleset.Rules.Count(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(1);
        GreaterRule(result, "Ring")!.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    // ---- Cyan rule when EffectiveTargetCap is 0 / targets are empty (a maxed unique-only slot). The
    // engine trivially marks matched(0) >= cap(0) as maxed, so a unique-only goal with zero affix targets
    // takes the cyan branch. Confirm the GreaterAffixCondition still attaches when a type gate exists, and
    // document what happens when it doesn't (no type gate + no affixes == no conditions at all, so
    // SlotRuleBuilder returns null and the GA requirement is silently lost). ----

    [Fact]
    public void Generate_MaxedZeroTargetResolvableSlot_ItemTypePlusGreaterOnlyRule_NoAffixCondition()
    {
        // Helm resolves to a real item type; zero targets + maxed (matched 0 >= cap 0) → the cyan rule
        // has an ItemTypeCondition + GreaterAffixCondition but NO AffixCondition at all.
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Helm, 0, 0, 0, 0)));

        var cyan = GreaterRule(result, "Helm").ShouldNotBeNull();
        cyan.Conditions.OfType<ItemTypeCondition>().ShouldHaveSingleItem();
        cyan.Conditions.OfType<AffixCondition>().ShouldBeEmpty();
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
        result.Warnings.ShouldNotContain(w => w.Contains("No conditions"));
    }

    [Fact]
    public void Generate_MaxedZeroTargetResolvableSlot_RoundTripsThroughCodec()
    {
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Helm, 0, 0, 0, 0)));
        var cyan = GreaterRule(result, "Helm").ShouldNotBeNull();

        var decoded = FilterCodec.Decode(FilterCodec.Encode(result.Ruleset));
        var decodedCyan = decoded.Rules.Single(r => r.Name == "Helm (Greater)");
        decodedCyan.Conditions.Select(c => c.GetType().Name)
            .ShouldBe(cyan.Conditions.Select(c => c.GetType().Name));
        decodedCyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_MaxedZeroTargetAmbiguousSlot_DroppedEntirely_GreaterAffixRequirementLost()
    {
        // Weapon (role None) resolves to NO item type AND has zero target affixes → BuildMultiType has
        // nothing to build a condition from and returns null, so the slot is skipped entirely — even
        // though IsMaxedOnTargets is true and the diff calls for an "at least 1 Greater Affix" rule. The
        // generic "Ambiguous item type" warning still fires and (misleadingly, in this specific case)
        // claims an affix-only rule is being emitted, when in fact NO rule is emitted for this slot.
        // Documents current behavior — flagged as a potential gap in the QA findings, not asserted correct.
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Weapon, 0, 0, 0, 0)));

        result.SlotRuleCount.ShouldBe(0);
        result.Warnings.ShouldContain(w => w.Contains("No conditions") && w.Contains("Weapon"));
        result.Warnings.ShouldContain(w => w.Contains("Ambiguous item type") && w.Contains("Weapon"));
        result.Ruleset.Rules.ShouldHaveSingleItem(); // Hide All only — the maxed slot vanishes silently
    }

    [Fact]
    public void Generate_MaxedSlot_NegativeEffectiveTargetCap_ClampsToOneNoThrow()
    {
        // A stale/hand-built diff could report a negative EffectiveTargetCap (never happens via
        // SlotDiffEngine, whose cap is Math.Min(targets.Count, rollableCap) — always >= 0). Confirm
        // SlotRuleBuilder's Math.Clamp(requiredCount, 1, capped.Count) floors it to 1 rather than
        // throwing or silently requesting a zero/negative affix count.
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Ring, 0, 0, 0, -3, 1u, 2u, 3u)));

        var cyan = GreaterRule(result, "Ring").ShouldNotBeNull();
        cyan.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_MaxedSlot_NegativeMatchedGreaterAffixCount_FloorsToOneNoThrow()
    {
        // Another stale-diff shape: MatchedGreaterAffixCount negative. Math.Max(1, matchedGa) must floor
        // it to 1 (a GreaterAffixCondition can't sensibly ask for < 1) — never throw, never emit <= 0.
        var result = NewGenerator().Generate(Diff(MaxedEquipped(GearSlot.Ring, 0, 2, -5, 2, 1u, 2u)));

        var cyan = GreaterRule(result, "Ring").ShouldNotBeNull();
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_EndToEnd_UniqueOnlyMaxedSlotWrongUnique_ResolvableSlot_EmitsCyanGaOnlyRule()
    {
        // Real SlotDiffEngine + RelativeToEquipped, not a hand-built diff: a Helm goal with a required
        // unique and ZERO affix targets, wrong unique equipped. effectiveCap = min(0, cap) = 0, so
        // matched(0) >= 0 trivially maxes the slot — NeedsRule fires purely off the unique mismatch, and
        // Generate() takes the cyan branch, producing an ItemType + GreaterAffixCondition-only rule.
        var loadout = EquippedLoadout.FromItems([Item(GearSlot.Helm, [], uniqueHash: 0xDEADu)]);
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Helm)] = new SlotGoal
            {
                TargetUnique = 0xBEEFu,
                TargetAffixIds = [],
                Threshold = MeetsGoalThreshold.RelativeToEquipped,
            },
        });
        var diff = new SlotDiffEngine().Diff(loadout, goal);
        var slotDiff = diff.Slots.Single();
        slotDiff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        slotDiff.IsMaxedOnTargets.ShouldBeTrue();
        slotDiff.UniqueSatisfied.ShouldBeFalse();

        var result = NewGenerator().Generate(diff);

        var cyan = GreaterRule(result, "Helm").ShouldNotBeNull();
        cyan.Conditions.OfType<ItemTypeCondition>().ShouldHaveSingleItem();
        cyan.Conditions.OfType<AffixCondition>().ShouldBeEmpty();
        cyan.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
        result.Ruleset.Rules.ShouldContain(r => r.Name == "Target Uniques"); // the unique gate still fires
    }

    [Fact]
    public void Generate_EndToEnd_UniqueOnlyMaxedSlotWrongUnique_AmbiguousWeaponSlot_SilentlyDropped()
    {
        // Same scenario as above but on an ambiguous slot (Weapon, no ItemTypeName so it keys role-less):
        // the maxed cyan branch has neither a type gate nor affix targets to build from, so the slot is
        // dropped entirely — the player gets NO highlighting for a slot that genuinely still needs the
        // correct unique. Confirms the gap found via hand-built diffs is reachable through the real
        // engine end-to-end, not just a synthetic SlotDiff.
        var loadout = EquippedLoadout.FromItems([Item(GearSlot.Weapon, [], uniqueHash: 0xDEADu)]);
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Weapon)] = new SlotGoal
            {
                TargetUnique = 0xBEEFu,
                TargetAffixIds = [],
                Threshold = MeetsGoalThreshold.RelativeToEquipped,
            },
        });
        var diff = new SlotDiffEngine().Diff(loadout, goal);
        var slotDiff = diff.Slots.Single();
        slotDiff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        slotDiff.IsMaxedOnTargets.ShouldBeTrue();

        var result = NewGenerator().Generate(diff);

        result.SlotRuleCount.ShouldBe(0);
        result.Ruleset.Rules.ShouldContain(r => r.Name == "Target Uniques"); // unique gate is unaffected
        result.Warnings.ShouldContain(w => w.Contains("No conditions") && w.Contains("Weapon"));
    }

    [Fact]
    public void Generate_PinkRules_DifferByMatchedGreaterAffixCountOnly_StillCollapse()
    {
        // Pink rules never attach a GreaterAffixCondition and never factor MatchedGreaterAffixCount into
        // the required count — so two otherwise-identical non-maxed slots with DIFFERENT matchedGreater
        // must still collapse to a single pink rule (ShapeKey is blind to GA count on the pink tier).
        var result = NewGenerator().Generate(Diff(
            NeedsRuleEquipped(GearSlot.Ring, 0, 1, 0, 500u),
            NeedsRuleEquipped(GearSlot.Ring, 1, 1, 3, 500u)));

        result.Ruleset.Rules.Count(r => r.Color == FilterColors.LightPurple).ShouldBe(1);
        result.SlotRuleCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_BudgetPressure_WithUniqueRule_MixedPinkCyan_DropsCyanFirst_CapacityShrunkByTwo()
    {
        // Capacity with a uniques rule is 23 (25 - Hide All - Target Uniques). 15 distinct pink (sharing
        // one target unique so the Uniques rule exists) + 15 distinct cyan == 30 raw rules. Pink ranks
        // first, so all 15 pink survive and only 8 of the 15 cyan fit (23 - 15), dropping the other 7 —
        // landing the total exactly on the 25-rule ceiling.
        var pink = Enumerable.Range(0, 15)
            .Select(i => NeedsUniqueRule(GearSlot.Helm, i, 0xAAAAu, (uint)(1000 + i)))
            .ToArray();
        var cyan = Enumerable.Range(0, 15)
            .Select(i => MaxedEquipped(GearSlot.Ring, i, 1, 0, 1, (uint)(2000 + i)))
            .ToArray();

        var result = NewGenerator().Generate(Diff(pink.Concat(cyan).ToArray()));

        result.BudgetExceeded.ShouldBeTrue();
        result.Ruleset.Rules.ShouldContain(r => r.Name == "Target Uniques");
        result.Warnings.Where(w => w.Contains("dropped")).ShouldAllBe(w => w.Contains("(Greater)"));
        result.Ruleset.Rules.Count(r => r.Color == FilterColors.LightPurple).ShouldBe(15);
        result.Ruleset.Rules.Count(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal))
            .ShouldBe(WithUniqueCapacity - 15); // 8 cyan survive
        result.Ruleset.Rules.Count.ShouldBe(FilterRuleset.MaxRuleCount); // lands exactly on the ceiling
    }

    // ---- Budget boundary: exact slack == 0, no uniques rule ----

    [Fact]
    public void Generate_NeedyEqualsCapacity_NoUnique_NoStrictRulesNoDrops()
    {
        var slots = Enumerable.Range(0, NoUniqueCapacity)
            .Select(i => NeedsRule(GearSlot.Helm, i, (uint)(1000 + i), 2u, 3u, 4u))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeFalse();
        result.Warnings.ShouldNotContain(w => w.Contains("dropped"));
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Show).ShouldBe(0); // all slot rules Recolor
        result.TotalRuleCount.ShouldBe(1 + NoUniqueCapacity);
        result.Ruleset.Rules.Count.ShouldBeLessThanOrEqualTo(FilterRuleset.MaxRuleCount);
    }

    [Fact]
    public void Generate_NeedyOneOverCapacity_NoUnique_DropsExactlyOne()
    {
        var slots = Enumerable.Range(0, NoUniqueCapacity + 1)
            .Select(i => NeedsRule(GearSlot.Helm, i, (uint)(1000 + i), 2u, 3u, 4u))
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
            .Select(i => NeedsUniqueRule(GearSlot.Helm, i, 0xAAAAu, (uint)(1000 + i), 2u, 3u, 4u))
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
            .Select(i => NeedsUniqueRule(GearSlot.Helm, i, 0xAAAAu, (uint)(1000 + i), 2u, 3u, 4u))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeTrue();
        result.Warnings.Count(w => w.Contains("dropped")).ShouldBe(1);
        result.Ruleset.Rules.Count.ShouldBe(1 + 1 + WithUniqueCapacity);
    }

    // ---- MatchedAffixCount boundaries: 0 (empty slot) is covered in ProgressionFilterGeneratorTests;
    // here we probe 1 (just above min), == target count (maxed but not dropped), and > target count
    // (a stale/hand-built diff — can't happen via SlotDiffEngine but the generator takes a public
    // SlotDiffResult so a caller could pass one anyway). ----

    [Fact]
    public void Generate_MatchedAffixCountOne_RequiredCountIsOne()
    {
        // Non-maxed with 1 matched target → the pink rule requires the SAME OR MORE count (max(1, 1) == 1),
        // not matched + 1.
        var diff = Diff(new SlotDiff
        {
            Slot = new SlotKey(GearSlot.Helm),
            Status = SlotDiffStatus.NeedsRule,
            TargetAffixIds = [1u, 2u, 3u, 4u],
            MatchedAffixCount = 1,
        });

        var result = NewGenerator().Generate(diff);

        result.Ruleset.Rules.Single(r => r.Name == "Helm")
            .Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_MatchedAffixCountEqualsTargetCount_StillNeedsRule_RequiresAllTargets()
    {
        // The equipped item already has every ranked affix but is NOT flagged maxed (e.g. a hand-built diff
        // whose unique gate is unmet, so the slot is NeedsRule). The non-maxed pink branch requires the SAME
        // OR MORE count (max(1, 4) == 4), clamped by SlotRuleBuilder to the 4 targets — an
        // unmatchable-by-affixes-alone rule that only the unique condition (elsewhere) can practically
        // satisfy. Lock it down rather than let it regress silently.
        var diff = Diff(new SlotDiff
        {
            Slot = new SlotKey(GearSlot.Amulet),
            Status = SlotDiffStatus.NeedsRule,
            Goal = new SlotGoal { TargetUnique = 0xCAFEu, TargetAffixIds = [1u, 2u, 3u, 4u] },
            TargetAffixIds = [1u, 2u, 3u, 4u],
            MatchedAffixCount = 4,
        });

        var result = NewGenerator().Generate(diff);

        var rule = result.Ruleset.Rules.Single(r => r.Name == "Amulet");
        rule.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(4);
        rule.Conditions.OfType<AffixCondition>().Single().AffixIds.Count.ShouldBe(4);
    }

    [Fact]
    public void Generate_MatchedAffixCountExceedsTargetCount_ClampsToTargetCountNoThrow()
    {
        // A hand-built/stale SlotDiff (e.g. gear changed between diff and generate without a re-diff)
        // could report more matches than there are targets. ProgressionFilterGenerator does not
        // validate this invariant itself — it relies on SlotRuleBuilder's Math.Clamp(requiredCount, 1,
        // capped.Count) to stay safe. Confirm the full Generate() path never throws and never asks for
        // more affixes than exist on the rule.
        var diff = Diff(new SlotDiff
        {
            Slot = new SlotKey(GearSlot.Helm),
            Status = SlotDiffStatus.NeedsRule,
            TargetAffixIds = [1u, 2u, 3u],
            MatchedAffixCount = 10, // > TargetAffixIds.Count
        });

        var result = NewGenerator().Generate(diff);

        var rule = result.Ruleset.Rules.Single(r => r.Name == "Helm");
        var affixCondition = rule.Conditions.OfType<AffixCondition>().Single();
        affixCondition.MinimumCount.ShouldBe(3);
        affixCondition.MinimumCount.ShouldBeLessThanOrEqualTo(affixCondition.AffixIds.Count);
    }

    // ---- Affix-count clamping on generated slot rules ----

    [Fact]
    public void Generate_SingleAffixSlot_RequiredCountClampsToAffixCount()
    {
        var diff = Diff(NeedsRule(GearSlot.Helm, 0, 1u)); // only one target affix

        var result = NewGenerator().Generate(diff);

        var baseRule = result.Ruleset.Rules.Single(r => r.Name == "Helm");
        baseRule.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
        result.Ruleset.Rules.ShouldNotContain(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_ManyTargetAffixes_KeepsAllUpToGameLimit()
    {
        // All ranked guide affixes are kept (D4 allows up to AffixCondition.MaxSelectionCount per
        // Required Affixes condition); the top-ranked affix is never dropped.
        var diff = Diff(NeedsRule(GearSlot.Helm, 0, 1u, 2u, 3u, 4u, 5u, 6u));

        var result = NewGenerator().Generate(diff);

        var baseRule = result.Ruleset.Rules.Single(r => r.Name == "Helm");
        baseRule.Conditions.OfType<AffixCondition>().Single().AffixIds
            .ShouldBe(new uint[] { 1u, 2u, 3u, 4u, 5u, 6u });
    }

    [Fact]
    public void Generate_TargetAffixesOverGameLimit_ClampsToMaxSelectionCount()
    {
        var affixes = Enumerable.Range(1, AffixCondition.MaxSelectionCount + 3).Select(i => (uint)i).ToArray();
        var diff = Diff(NeedsRule(GearSlot.Helm, 0, affixes));

        var result = NewGenerator().Generate(diff);

        var baseRule = result.Ruleset.Rules.Single(r => r.Name == "Helm");
        baseRule.Conditions.OfType<AffixCondition>().Single().AffixIds
            .Count.ShouldBe(AffixCondition.MaxSelectionCount);
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
    public void Generate_ZeroAffixResolvableSlot_ItemTypeOnlyRuleRoundTripsThroughCodec()
    {
        // Zero affixes + a resolvable slot → an ItemTypeCondition-only rule (no AffixCondition at all) —
        // an unusual shape worth a dedicated round-trip check.
        var diff = Diff(NeedsRule(GearSlot.Helm, 0));

        var result = NewGenerator().Generate(diff);

        var rule = result.Ruleset.Rules.Single(r => r.Name == "Helm");
        rule.Conditions.OfType<AffixCondition>().ShouldBeEmpty();
        rule.Conditions.OfType<ItemTypeCondition>().ShouldHaveSingleItem();

        var decoded = FilterCodec.Decode(FilterCodec.Encode(result.Ruleset));
        var decodedRule = decoded.Rules.Single(r => r.Name == "Helm");
        decodedRule.Conditions.Select(c => c.GetType().Name)
            .ShouldBe(rule.Conditions.Select(c => c.GetType().Name));
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

    // ---- Weapon/Offhand role gating: multi-type expansion, Offhand symmetry ----

    [Fact]
    public void ProgressionFilter_gates_offhand_role_on_multi_type()
    {
        // Symmetric to the Weapon coverage in ProgressionFilterGeneratorTests — an Offhand role resolves
        // to the class's offhand type set (Focus + 1H weapons for a Sorcerer).
        var resolver = new NameResolver(new FilterDataService());
        resolver.TryResolveItemType("Focus", out var focusHash, out _).ShouldBeTrue();

        var result = new ProgressionFilterGenerator(resolver, new WeaponRoleMap(resolver))
            .Generate(Diff(NeedsRule(GearSlot.Offhand, WeaponSlotRole.Offhand, 1u, 2u)), PlayerClass.Sorcerer);

        var rule = result.Ruleset.Rules.Single(r => r.Name == "Offhand");
        rule.Conditions.OfType<ItemTypeCondition>().Single().TypeIds.ShouldContain(focusHash);
        result.Warnings.ShouldNotContain(w => w.Contains("Ambiguous item type"));
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

    // ---- Empty loadout through the real diff engine (every goal slot needs a rule) ----
    // The all-slots-complete corner is covered by Generate_AllSlotsMeetGoalViaRealEngine_OnlyHideAll
    // above; the >25-overflow corner by the NeedyOneOverCapacity_* tests — not duplicated here.

    [Fact]
    public void Generate_EmptyLoadoutWithGoals_AllSlotsNeedRules_NoneDropped()
    {
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Helm)] = new() { TargetAffixIds = [1u, 2u] },
            [new SlotKey(GearSlot.Gloves)] = new() { TargetAffixIds = [1u, 2u] },
        });
        var diff = new SlotDiffEngine().Diff(EquippedLoadout.FromItems([]), goal);

        // With no gear, both goal slots are unmet and every slot's diff says so.
        diff.Slots.Count.ShouldBe(2);
        diff.Slots.ShouldAllBe(s => s.Status == SlotDiffStatus.NeedsRule);
        diff.Slots.ShouldAllBe(s => s.Notes.Contains("no gear equipped"));

        var result = NewGenerator().Generate(diff);

        result.BudgetExceeded.ShouldBeFalse();
        result.Warnings.ShouldNotContain(w => w.Contains("dropped"));
        result.Ruleset.Rules.ShouldContain(r => r.Name == "Helm");
        result.Ruleset.Rules.ShouldContain(r => r.Name == "Gloves");

        // One Recolor rule per unmet slot + Hide All = 3; no "(Greater)" rules.
        result.Ruleset.Rules.ShouldNotContain(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal));
        result.TotalRuleCount.ShouldBe(2 + 1);
    }

    [Fact]
    public void Generate_EmptyLoadoutNoGoals_OnlyHideAll()
    {
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>());
        var diff = new SlotDiffEngine().Diff(EquippedLoadout.FromItems([]), goal);

        var result = NewGenerator().Generate(diff);

        result.Ruleset.Rules.ShouldHaveSingleItem();
        result.Ruleset.Rules[0].Visibility.ShouldBe(Visibility.HideAll);
        result.Warnings.ShouldBeEmpty();
    }

    // ---- Collapse-before-cap interaction: the generator must collapse byte-identical rules BEFORE
    // applying the 25-rule budget, so raw needy-slot counts that exceed the ceiling can still fit once
    // duplicates merge. ----

    [Fact]
    public void Generate_CollapseBringsRawCountUnderCap_NoDropsEvenThoughRawExceedsCapacity()
    {
        // 3 Ring ordinals share an identical shape (same resolved "Ring" item-type hash + identical
        // affix targets/min-count) and collapse to ONE rule; 23 more Helm slots each carry a distinct
        // affix set so they stay separate. Raw needy count is 26 (> the 24-rule no-unique capacity), but
        // the COLLAPSED count is exactly 24 — if collapse ran after the cap (or not at all) this would
        // incorrectly drop 2 slots and warn; because collapse runs first, nothing is dropped.
        var duplicateShapeRings = Enumerable.Range(0, 3)
            .Select(i => NeedsRule(GearSlot.Ring, i, 10u, 20u))
            .ToArray();
        var distinctHelms = Enumerable.Range(0, 23)
            .Select(i => NeedsRule(GearSlot.Helm, i, (uint)(1000 + i), 2u, 3u, 4u))
            .ToArray();
        var slots = duplicateShapeRings.Concat(distinctHelms).ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeFalse();
        result.Warnings.ShouldNotContain(w => w.Contains("dropped"));
        result.SlotRuleCount.ShouldBe(NoUniqueCapacity); // 1 collapsed Ring rule + 23 distinct Helm rules == 24
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Recolor).ShouldBe(NoUniqueCapacity);
    }

    [Fact]
    public void Generate_CollapsedCountOneOverCap_DropsExactlyOneCollapsedGroup_LastInOrder()
    {
        // Same shape as above, but with 24 distinct Helm slots instead of 23 → 1 collapsed Ring rule + 24
        // distinct Helm rules = 25 collapsed groups, one over the 24-rule no-unique capacity. Exactly one
        // group is dropped, and it must be the LAST one in slot order (Helm ordinal 23 → "Helm#24"), not
        // an arbitrary or hash-ordered pick.
        var duplicateShapeRings = Enumerable.Range(0, 3)
            .Select(i => NeedsRule(GearSlot.Ring, i, 10u, 20u))
            .ToArray();
        var distinctHelms = Enumerable.Range(0, 24)
            .Select(i => NeedsRule(GearSlot.Helm, i, (uint)(1000 + i), 2u, 3u, 4u))
            .ToArray();
        var slots = duplicateShapeRings.Concat(distinctHelms).ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeTrue();
        result.Warnings.Count(w => w.Contains("dropped")).ShouldBe(1);
        result.Warnings.Single(w => w.Contains("dropped")).ShouldContain("Helm#24");
        result.SlotRuleCount.ShouldBe(NoUniqueCapacity);
    }

    [Fact]
    public void Generate_RingPool_IgnoresOrdinal_NamedRingRegardlessOfPosition()
    {
        // Two Ring slots sharing the same targets, at ordinals 5 and 2 (in that input order, separated by
        // an unrelated Helm), are an interchangeable pool keyed only on the shared item-type gate — ordinal
        // plays no part in grouping or naming, so the single surviving rule is plainly "Ring" (never
        // "Ring#6" or "Ring#3").
        var diff = Diff(
            NeedsRule(GearSlot.Ring, 5, 10u, 20u),
            NeedsRule(GearSlot.Helm, 0, 99u),
            NeedsRule(GearSlot.Ring, 2, 10u, 20u));

        var result = NewGenerator().Generate(diff);

        result.SlotRuleCount.ShouldBe(2);
        result.Ruleset.Rules.ShouldContain(r => r.Name == "Ring");
        result.Ruleset.Rules.ShouldNotContain(r => r.Name == "Ring#6" || r.Name == "Ring#3");
    }

    [Fact]
    public void Generate_CollapseAcrossBothTiers_RawExceedsCapButCollapsedFitsExactly_NoDrops()
    {
        // Collapse-then-cap must merge byte-identical rules within EACH tier (pink + cyan) before the
        // 24-rule budget is applied. 13 identical non-maxed Rings collapse to 1 pink; 13 identical maxed
        // Amulets collapse to 1 cyan (26 RAW rules already over the no-unique capacity on their own). Adding
        // 11 distinct non-maxed Helms (11 pink) and 11 distinct maxed Boots (11 cyan) brings the COLLAPSED
        // total to (1+11) pink + (1+11) cyan == 24 — so if collapse ran after the cap this would spuriously
        // drop rules; because collapse runs first across both tiers, nothing drops.
        var identicalPinkRings = Enumerable.Range(0, 13)
            .Select(i => NeedsRuleEquipped(GearSlot.Ring, i, 1, 0, 500u))
            .ToArray();
        var identicalCyanAmulets = Enumerable.Range(0, 13)
            .Select(i => MaxedEquipped(GearSlot.Amulet, i, 1, 0, 1, 600u))
            .ToArray();
        var distinctPinkHelms = Enumerable.Range(0, 11)
            .Select(i => NeedsRuleEquipped(GearSlot.Helm, i, 1, 0, (uint)(1000 + i)))
            .ToArray();
        var distinctCyanBoots = Enumerable.Range(0, 11)
            .Select(i => MaxedEquipped(GearSlot.Boots, i, 1, 0, 1, (uint)(2000 + i)))
            .ToArray();

        var slots = identicalPinkRings
            .Concat(identicalCyanAmulets)
            .Concat(distinctPinkHelms)
            .Concat(distinctCyanBoots)
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeFalse();
        result.Warnings.ShouldNotContain(w => w.Contains("dropped"));
        result.SlotRuleCount.ShouldBe(NoUniqueCapacity); // (1+11) pink + (1+11) cyan == 24
        result.Ruleset.Rules.Count(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(12);
        result.Ruleset.Rules.Count(r => r.Color == FilterColors.LightPurple).ShouldBe(12);
        result.Ruleset.Rules.ShouldContain(r => r.Name == "Ring");            // the collapsed pink ring survives
        result.Ruleset.Rules.ShouldContain(r => r.Name == "Amulet (Greater)"); // and the collapsed cyan amulet
    }

    // ---- Class-filter disagreement (QA-pinned): RoleForItemType classifies purely by item shape and
    // ignores entry.Classes, while AllowedTypeHashes DOES apply ClassMatch. For a class with no catalog
    // weapon of that type (Spiritborn), an equipped weapon still keys to a concrete role, but the
    // generator's type gate for that role comes back EMPTY — yielding an affix-only rule + ambiguity
    // warning rather than a type-gated one. Documents the disagreement's end-to-end consequence. ----

    [Fact]
    public void Generate_SpiritbornEquippedSword_RoleKeyedButEmptyGate_AffixOnlyRuleWithAmbiguityWarning()
    {
        var roleMap = RoleMap();
        var mainhandKey = new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand);

        // RoleForItemType("Sword", Spiritborn) == Mainhand (no class check), so FromItems keys the sword by
        // role even though Spiritborn owns no catalog Sword.
        var sword = Item(GearSlot.Weapon, [], itemTypeName: "Sword");
        var loadout = EquippedLoadout.FromItems([sword], PlayerClass.Spiritborn, roleMap);
        loadout.Items.ShouldContainKey(mainhandKey);

        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [mainhandKey] = new() { TargetAffixIds = [1u, 2u] },
        });
        var diff = new SlotDiffEngine().Diff(loadout, goal);

        var result = NewGenerator().Generate(diff, PlayerClass.Spiritborn);

        // AllowedTypeHashes(Mainhand, Spiritborn) is empty → the role's rule has no ItemTypeCondition gate.
        var rule = result.Ruleset.Rules.Single(r => r.Name == "Mainhand");
        rule.Conditions.OfType<ItemTypeCondition>().ShouldBeEmpty();
        rule.Conditions.OfType<AffixCondition>().ShouldHaveSingleItem();
        result.Warnings.ShouldContain(w => w.Contains("Ambiguous item type") && w.Contains("Mainhand"));
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
            var result = generator.Generate(diff, filterName: $"Filter {i}");
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

    // ---- Interchangeable pools, part 2: 3+ member pools, the non-poolable negative control, mixed
    // pink/cyan-scale membership, many-group cap/name interaction, and the Flail catalog addition's
    // effect on the pooled item-type gate. Targets the "highest-risk" gaps the pooling change's own
    // handoff called out as unverified beyond the 2-member (rings/Barb hands) cases already covered in
    // ProgressionFilterGeneratorTests. ----

    [Fact]
    public void Generate_ThreeRingPool_PinkBranch_WorstOfThreeNotJustPairMin()
    {
        // Three rings share one goal; matched counts 3, 1, 2 — the pool must key to the true minimum
        // across all THREE members (1), not e.g. the min of only the first two encountered (would be 1
        // too by luck) or the last two (2). Distinct per-member counts make the reduction unambiguous.
        var result = NewGenerator().Generate(Diff(
            NeedsRuleEquipped(GearSlot.Ring, 0, 3, 0, 10u, 20u),
            NeedsRuleEquipped(GearSlot.Ring, 1, 1, 0, 10u, 20u),
            NeedsRuleEquipped(GearSlot.Ring, 2, 2, 0, 10u, 20u)));

        var pink = result.Ruleset.Rules.Where(r => r.Name == "Ring").ToList();
        pink.ShouldHaveSingleItem();
        pink[0].Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
        result.SlotRuleCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_ThreeRingPool_CyanBranch_WorstGaOfThreeNotFlooredByCoincidence()
    {
        // Three maxed rings sharing targets/cap with GA counts 5, 3, 2 — min is 2, which only a true
        // 3-way reduction proves; a buggy pairwise-only reduction (or one that used the first/last
        // member) could still coincidentally floor to 1 with smaller numbers, so this uses values where
        // the correct answer (2) is distinguishable from every wrong answer (5, 3, 1).
        var result = NewGenerator().Generate(Diff(
            MaxedEquipped(GearSlot.Ring, 0, 2, 5, 2, 10u, 20u),
            MaxedEquipped(GearSlot.Ring, 1, 2, 3, 2, 10u, 20u),
            MaxedEquipped(GearSlot.Ring, 2, 2, 2, 2, 10u, 20u)));

        result.Ruleset.Rules.Count(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(1);
        GreaterRule(result, "Ring")!.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(2);
    }

    [Fact]
    public void Generate_TwoHelmOrdinals_NeverPool_KeyedSeparatelyByOrdinal()
    {
        // Negative control for pool-key collision (armor slots are never poolable, unlike Ring/weapon
        // roles): two Helm ordinals resolve to the IDENTICAL item-type hash (ResolveTypeHashes ignores
        // ordinal), so a pooling bug that keyed purely on resolved type hashes would merge them. Giving
        // the two DIFFERENT matched counts (2 vs 1) also yields different required counts, so the two
        // rules have different shapes — the byte-identical-shape collapse step cannot merge them either,
        // isolating this as a pure pooling-stage assertion.
        var result = NewGenerator().Generate(Diff(
            NeedsRuleEquipped(GearSlot.Helm, 0, 2, 0, 1u, 2u, 3u, 4u),
            NeedsRuleEquipped(GearSlot.Helm, 1, 1, 0, 1u, 2u, 3u, 4u)));

        result.SlotRuleCount.ShouldBe(2);
        result.Ruleset.Rules.Single(r => r.Name == "Helm")
            .Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(2);
        result.Ruleset.Rules.Single(r => r.Name == "Helm#2")
            .Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_MixedMaxedAndNonMaxedRingPool_PinkBranch_MinCrossesBothRegimeScales()
    {
        // One ring is maxed (matched 5, cap 3 — a large/"maxed" scale of MatchedAffixCount) and its pool
        // partner is not maxed (matched 1). Any(!IsMaxedOnTargets) is true, so the WHOLE group takes the
        // pink branch even though one member individually qualifies for cyan — and the pink required
        // count must reduce across BOTH members' MatchedAffixCount despite the very different scales,
        // landing on the true min (1), not the maxed member's larger value.
        var result = NewGenerator().Generate(Diff(
            MaxedEquipped(GearSlot.Ring, 0, 5, 2, 3, 10u, 20u, 30u),
            NeedsRuleEquipped(GearSlot.Ring, 1, 1, 0, 10u, 20u, 30u)));

        result.Ruleset.Rules.ShouldNotContain(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal));
        var pink = result.Ruleset.Rules.Single(r => r.Name == "Ring");
        pink.Color.ShouldBe(FilterColors.LightPurple);
        pink.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
        result.SlotRuleCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_ManyDistinctGoalRingGroups_OverCap_DropsLastGroupByPoolLabelNotOrdinal()
    {
        // 25 rings, each with a DISTINCT single target affix, all share the Ring pool but split into 25
        // separate (pool, signature) groups — named "Ring", "Ring 2", ... "Ring 25" — one over the
        // 24-rule no-unique capacity. The dropped warning must name the POOL-GROUP label ("Ring 25"),
        // never a stale per-instance ordinal name (e.g. "Ring#25"), confirming the cap/warning path was
        // updated for pooled naming, not just left pointing at slot.Slot.ToString().
        var slots = Enumerable.Range(0, 25)
            .Select(i => NeedsRule(GearSlot.Ring, i, (uint)(3000 + i)))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeTrue();
        result.Warnings.Count(w => w.Contains("dropped")).ShouldBe(1);
        result.Warnings.Single(w => w.Contains("dropped")).ShouldContain("Ring 25");
        result.Ruleset.Rules.ShouldNotContain(r => r.Name.Contains('#'));
        result.SlotRuleCount.ShouldBe(NoUniqueCapacity);
    }

    [Fact]
    public void ProgressionFilter_barb_dual_flail_pool_gate_includes_flail_hash()
    {
        // The Flail catalog addition (classes: Barbarian/Warlock/Necromancer/Paladin/Druid) must actually
        // reach the pooled Barbarian dual-1H-hands item-type gate: Mainhand and Offhand resolve to the
        // SAME class-aware 1H type set (which now includes Flail), so they pool into one rule whose gate
        // contains the Flail hash, keyed to the worse hand's matched count.
        var resolver = new NameResolver(new FilterDataService());
        resolver.TryResolveItemType("Flail", out var flailHash, out _).ShouldBeTrue();

        var result = new ProgressionFilterGenerator(resolver, new WeaponRoleMap(resolver)).Generate(
            Diff(
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
                }),
            PlayerClass.Barbarian);

        var recolor = result.Ruleset.Rules.Where(r => r.Visibility == Visibility.Recolor).ToList();
        recolor.ShouldHaveSingleItem();
        recolor[0].Conditions.OfType<ItemTypeCondition>().Single().TypeIds.ShouldContain(flailHash);
        recolor[0].Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1); // worst hand
    }
}

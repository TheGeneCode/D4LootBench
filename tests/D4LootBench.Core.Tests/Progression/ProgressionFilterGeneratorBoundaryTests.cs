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

    // A NeedsRule diff with a real equipped item so the Greater-Affix companion gate (EquippedItem != null)
    // can fire; matched/matchedGreater are set directly to probe the companion's emission boundaries.
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

    private static FilterRule? GreaterRule(ProgressionFilterResult result, string slotName) =>
        result.Ruleset.Rules.FirstOrDefault(r => r.Name == slotName + " (Greater)");

    // ---- Greater-Affix companion rule: emission gate, shape, ordering, budget priority ----

    [Fact]
    public void Generate_EquippedWithNonGreaterMatch_EmitsCyanGreaterCompanion()
    {
        // Equipped ring has 2 of its 3 targets, none greater → base rule requires 3 (an affix upgrade),
        // and a cyan companion catches an item with the SAME 2 affixes but one Greater Affix.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Ring, 0, 2, 0, 1u, 2u, 3u)));

        var baseRule = result.Ruleset.Rules.Single(r => r.Name == "Ring");
        baseRule.Color.ShouldBe(FilterRule.PackColor(255, 180, 0));
        baseRule.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(3);
        baseRule.Conditions.OfType<GreaterAffixCondition>().ShouldBeEmpty();

        var greater = GreaterRule(result, "Ring").ShouldNotBeNull();
        greater.Visibility.ShouldBe(Visibility.Recolor);
        greater.Color.ShouldBe(FilterRule.PackColor(0, 220, 255));
        greater.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(2); // same count as equipped
        greater.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1); // one more GA than 0
    }

    [Fact]
    public void Generate_GreaterCompanion_RequiresOneMoreGaThanEquipped()
    {
        // Equipped has 3 matched, 1 already greater → companion demands 2 GA (one more) at the same count.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Amulet, 0, 3, 1, 1u, 2u, 3u, 4u)));

        var greater = GreaterRule(result, "Amulet").ShouldNotBeNull();
        greater.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(3);
        greater.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(2);
    }

    [Fact]
    public void Generate_AllMatchedAlreadyGreater_NoGreaterCompanion()
    {
        // matchedGreater == matched → no room to add a GA among the matched targets → no companion emitted.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Ring, 0, 2, 2, 1u, 2u, 3u)));

        GreaterRule(result, "Ring").ShouldBeNull();
        result.Ruleset.Rules.ShouldNotContain(r => r.Conditions.OfType<GreaterAffixCondition>().Any());
    }

    [Fact]
    public void Generate_NoEquippedItem_NoGreaterCompanionEvenWithMatchedCount()
    {
        // A hand-built diff reporting matches but NO equipped item (matchedGreater 0 < matched 2) must not
        // emit a companion — the gate requires a real item to beat, not just a matched count.
        var diff = Diff(new SlotDiff
        {
            Slot = new SlotKey(GearSlot.Ring),
            Status = SlotDiffStatus.NeedsRule,
            TargetAffixIds = [1u, 2u, 3u],
            MatchedAffixCount = 2,
            MatchedGreaterAffixCount = 0,
        });

        var result = NewGenerator().Generate(diff);

        GreaterRule(result, "Ring").ShouldBeNull();
        result.Ruleset.Rules.ShouldNotContain(r => r.Conditions.OfType<GreaterAffixCondition>().Any());
    }

    [Fact]
    public void Generate_GreaterCompanion_RoundTripsThroughCodec()
    {
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Ring, 0, 2, 0, 1u, 2u, 3u)));
        var greater = GreaterRule(result, "Ring").ShouldNotBeNull();

        var decoded = FilterCodec.Decode(FilterCodec.Encode(result.Ruleset));
        var decodedGreater = decoded.Rules.Single(r => r.Name == "Ring (Greater)");
        decodedGreater.Conditions.Select(c => c.GetType().Name)
            .ShouldBe(greater.Conditions.Select(c => c.GetType().Name));
        decodedGreater.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_AllBaseRulesRankAboveAllGreaterCompanions()
    {
        var result = NewGenerator().Generate(Diff(
            NeedsRuleEquipped(GearSlot.Helm, 0, 1, 0, 1u, 2u),
            NeedsRuleEquipped(GearSlot.Gloves, 0, 1, 0, 3u, 4u)));

        var names = result.Ruleset.Rules.Select(r => r.Name).ToList();
        var lastBase = Math.Max(names.IndexOf("Helm"), names.IndexOf("Gloves"));
        var firstGreater = Math.Min(names.IndexOf("Helm (Greater)"), names.IndexOf("Gloves (Greater)"));
        lastBase.ShouldBeLessThan(firstGreater); // every base rule precedes every companion
    }

    [Fact]
    public void Generate_BudgetPressure_DropsGreaterCompanionsBeforeBaseRules()
    {
        // 24 distinct equipped slots → 24 base + 24 companion = 48 rules for a 24-rule (no-unique) capacity.
        // Base rules rank first, so all 24 base rules survive and all 24 companions are dropped.
        var slots = Enumerable.Range(0, NoUniqueCapacity)
            .Select(i => NeedsRuleEquipped(GearSlot.Helm, i, 1, 0, (uint)(1000 + i)))
            .ToArray();

        var result = NewGenerator().Generate(Diff(slots));

        result.BudgetExceeded.ShouldBeTrue();
        result.Ruleset.Rules.Count(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(0);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Recolor).ShouldBe(NoUniqueCapacity);
        result.Warnings.Count(w => w.Contains("dropped") && w.Contains("(Greater)")).ShouldBe(NoUniqueCapacity);
    }

    // ---- Greater-Affix companion: matched/matchedGreater edge combinations beyond the nominal
    // matched=2/3 cases above (gate is `EquippedItem is not null && MatchedGreaterAffixCount <
    // MatchedAffixCount`; the companion's own required-affix-count has NO Math.Max(1, ...) floor like
    // the base rule does — it relies solely on SlotRuleBuilder's clamp). ----

    [Fact]
    public void Generate_MatchedZero_EquippedItemPresent_GateFalse_NoCompanion()
    {
        // matchedGreater(0) < matched(0) is false — the "must have room to gain a GA" gate must reject
        // this even though an equipped item exists, since there's nothing matched to begin with.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Ring, 0, 0, 0, 1u, 2u)));

        GreaterRule(result, "Ring").ShouldBeNull();
        result.Ruleset.Rules.ShouldNotContain(r => r.Conditions.OfType<GreaterAffixCondition>().Any());
    }

    [Fact]
    public void Generate_MatchedOneMatchedGreaterZero_CompanionRequiresOneAffixOneGa()
    {
        // Smallest nonzero matched count: gate 0 < 1 true. Companion required-affix-count clamps to the
        // single target (1) and the GA requirement is just one (0 + 1) — the minimal viable companion.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Ring, 0, 1, 0, 1u)));

        var greater = GreaterRule(result, "Ring").ShouldNotBeNull();
        greater.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
        greater.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_MatchedGreaterOneBelowMatched_CompanionRequiresAllMatchedToBeGreater()
    {
        // matchedGreater == matched - 1 (the last boundary before "AllMatchedAlreadyGreater" suppresses
        // the companion entirely): GA requirement becomes matchedGreater + 1 == matched, i.e. every
        // currently-matched affix must be greater — the strictest satisfiable companion shape.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Ring, 0, 2, 1, 1u, 2u, 3u)));

        var greater = GreaterRule(result, "Ring").ShouldNotBeNull();
        greater.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(2);
        greater.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(2);
    }

    [Fact]
    public void Generate_MatchedGreaterExceedsMatched_StaleDiff_NoCompanionNoThrow()
    {
        // A hand-built/stale SlotDiff could report MORE greater matches than matches (shouldn't happen via
        // SlotDiffEngine, but Generate() takes a public SlotDiffResult). Gate (matchedGreater < matched)
        // is false, so no companion — and the generator must not throw on this out-of-range combination.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Ring, 0, 1, 5, 1u, 2u)));

        GreaterRule(result, "Ring").ShouldBeNull();
        result.Ruleset.Rules.ShouldNotContain(r => r.Conditions.OfType<GreaterAffixCondition>().Any());
    }

    [Fact]
    public void Generate_GreaterCompanion_MatchedExceedsTargetCount_ClampsAffixMinNoThrow()
    {
        // Companion-specific variant of the base-rule clamp test above: a stale diff reporting more
        // matches than there are targets must not overflow the companion's AffixCondition either —
        // SlotRuleBuilder clamps requiredCount to [1, capped.Count] regardless of caller input.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Helm, 0, 10, 0, 1u, 2u, 3u)));

        var greater = GreaterRule(result, "Helm").ShouldNotBeNull();
        var affixCondition = greater.Conditions.OfType<AffixCondition>().Single();
        affixCondition.MinimumCount.ShouldBe(3);
        affixCondition.MinimumCount.ShouldBeLessThanOrEqualTo(affixCondition.AffixIds.Count);
        greater.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_AmbiguousSlotWithEquippedItem_AffixOnlyGreaterCompanionRoundTrips()
    {
        // Weapon/Offhand with role None resolves to an EMPTY type-hash set (ambiguous), so the companion
        // has no ItemTypeCondition at all — just AffixCondition + GreaterAffixCondition. This shape isn't
        // covered by the resolvable-slot round-trip test above; confirm it survives encode/decode too.
        var result = NewGenerator().Generate(Diff(NeedsRuleEquipped(GearSlot.Weapon, 0, 2, 0, 1u, 2u, 3u)));

        var greater = GreaterRule(result, "Weapon").ShouldNotBeNull();
        greater.Conditions.OfType<ItemTypeCondition>().ShouldBeEmpty();
        greater.Conditions.OfType<AffixCondition>().ShouldHaveSingleItem();
        greater.Conditions.OfType<GreaterAffixCondition>().ShouldHaveSingleItem();

        var decoded = FilterCodec.Decode(FilterCodec.Encode(result.Ruleset));
        var decodedGreater = decoded.Rules.Single(r => r.Name == "Weapon (Greater)");
        decodedGreater.Conditions.Select(c => c.GetType().Name)
            .ShouldBe(greater.Conditions.Select(c => c.GetType().Name));
        decodedGreater.Conditions.OfType<GreaterAffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Generate_IdenticalShapeCompanions_CollapseToOne()
    {
        // Two Ring ordinals sharing the same resolved item type, targets, matched, AND matchedGreater
        // produce byte-identical companion rules (same ga:{n} shape key) — collapse must merge them just
        // like base rules do (e.g. Barbarian dual-1H hands sharing a companion shape).
        var result = NewGenerator().Generate(Diff(
            NeedsRuleEquipped(GearSlot.Ring, 0, 2, 0, 10u, 20u),
            NeedsRuleEquipped(GearSlot.Ring, 1, 2, 0, 10u, 20u)));

        result.Ruleset.Rules.Count(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(1);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Recolor
            && !r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public void Generate_DifferingMatchedGreaterCompanions_DoNotCollapse()
    {
        // Same base shape (same type/targets/matched) but different matchedGreater → the ShapeKey's
        // ga:{MinimumCount} segment differs, so both companions must survive collapse even though the
        // base rules (which don't depend on matchedGreater) DO collapse to one.
        var result = NewGenerator().Generate(Diff(
            NeedsRuleEquipped(GearSlot.Ring, 0, 2, 0, 10u, 20u),
            NeedsRuleEquipped(GearSlot.Ring, 1, 2, 1, 10u, 20u)));

        result.Ruleset.Rules.Count(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(2);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Recolor
            && !r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(1);
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
    public void Generate_MatchedAffixCountOne_RequiredCountIsTwo()
    {
        var diff = Diff(new SlotDiff
        {
            Slot = new SlotKey(GearSlot.Helm),
            Status = SlotDiffStatus.NeedsRule,
            TargetAffixIds = [1u, 2u, 3u, 4u],
            MatchedAffixCount = 1,
        });

        var result = NewGenerator().Generate(diff);

        result.Ruleset.Rules.Single(r => r.Name == "Helm")
            .Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(2);
    }

    [Fact]
    public void Generate_MatchedAffixCountEqualsTargetCount_StillNeedsRule_RequiresAllTargets()
    {
        // The equipped item already has every ranked affix (e.g. a unique gate is still unmet, so the
        // slot is NeedsRule rather than MeetsGoal). requiredCount = count+1 clamps down to count in
        // SlotRuleBuilder, so the rule ends up requiring ALL targets — an unmatchable-by-affixes-alone
        // rule that only the unique condition (elsewhere) can practically satisfy. This is the exact
        // scenario the QA brief calls out; lock it down rather than let it regress silently.
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
    public void Generate_CollapseOrderStable_FirstOccurrenceNameSurvivesRegardlessOfOrdinalMagnitude()
    {
        // The duplicate (same shape as the first Ring slot) appears LAST in slot order but at a SMALLER
        // ordinal (2 < 5). Collapse must key off first-occurrence-in-input-order, not ordinal/name
        // sorting — so the survivor is named after ordinal 5 ("Ring#6"), and the ordinal-2 duplicate
        // ("Ring#3") never appears despite being numerically "earlier".
        var diff = Diff(
            NeedsRule(GearSlot.Ring, 5, 10u, 20u),
            NeedsRule(GearSlot.Helm, 0, 99u),
            NeedsRule(GearSlot.Ring, 2, 10u, 20u));

        var result = NewGenerator().Generate(diff);

        result.SlotRuleCount.ShouldBe(2);
        result.Ruleset.Rules.ShouldContain(r => r.Name == "Ring#6");
        result.Ruleset.Rules.ShouldNotContain(r => r.Name == "Ring#3");
    }

    [Fact]
    public void Generate_CollapseWithBothTiers_RawExceedsCapButCollapsedFitsExactly_NoDrops()
    {
        // Collapse-then-cap must merge byte-identical rules across BOTH tiers (base + greater companion)
        // before the 24-rule budget is applied. 13 identical equipped Rings raise 13 base + 13 greater = 26
        // RAW rules (already over the 24 no-unique capacity on their own), yet collapse to just 1 base + 1
        // greater. Adding 11 distinct equipped Helms (11 base + 11 greater, all distinct) brings the
        // COLLAPSED total to exactly 24 — so if collapse ran after the cap (or ignored the greater tier)
        // this would spuriously drop rules; because collapse runs first across both tiers, nothing drops.
        var identicalRings = Enumerable.Range(0, 13)
            .Select(i => NeedsRuleEquipped(GearSlot.Ring, i, 1, 0, 500u))
            .ToArray();
        var distinctHelms = Enumerable.Range(0, 11)
            .Select(i => NeedsRuleEquipped(GearSlot.Helm, i, 1, 0, (uint)(1000 + i)))
            .ToArray();

        var result = NewGenerator().Generate(Diff(identicalRings.Concat(distinctHelms).ToArray()));

        result.BudgetExceeded.ShouldBeFalse();
        result.Warnings.ShouldNotContain(w => w.Contains("dropped"));
        result.SlotRuleCount.ShouldBe(NoUniqueCapacity); // (1 ring + 11 helm) base + (1 ring + 11 helm) greater == 24
        result.Ruleset.Rules.Count(r => r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(12);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.Recolor
            && !r.Name.EndsWith("(Greater)", StringComparison.Ordinal)).ShouldBe(12);
        result.Ruleset.Rules.ShouldContain(r => r.Name == "Ring");           // the collapsed ring base survives
        result.Ruleset.Rules.ShouldContain(r => r.Name == "Ring (Greater)"); // and its collapsed companion
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
}

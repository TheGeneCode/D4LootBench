namespace D4LootBench.Core.Tests.Progression;

using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Models;
using D4LootBench.Core.Progression;
using Shouldly;
using Xunit;

// Boundary/edge-case coverage for SlotDiffEngine beyond the plan-table happy paths already
// covered in SlotDiffEngineTests. Focused on the "highest-risk edge cases" the implementer
// flagged: empty target sets, zero/negative thresholds, duplicate-hash GA folding, ceiling
// exactness, unique+affix note independence, and the engine's own default threshold.
public class SlotDiffEngineBoundaryTests
{
    private const uint A = 0x1001;
    private const uint B = 0x1002;
    private const uint C = 0x1003;
    private const uint UniqueU = 0x2001;

    // Diffs a single Helm slot: builds a one-item loadout and a one-goal build, runs the engine,
    // and returns that slot's diff. Mirrors the helper in SlotDiffEngineTests (kept local so this
    // file has no cross-file dependency).
    private static SlotDiff DiffHelm(GearItem? item, SlotGoal goal, SlotDiffEngine? engine = null)
    {
        var items = new Dictionary<SlotKey, GearItem>();
        if (item is not null)
        {
            items[new SlotKey(GearSlot.Helm)] = item;
        }

        var loadout = new EquippedLoadout(items);
        var build = new GoalBuild(new Dictionary<SlotKey, SlotGoal> { [new SlotKey(GearSlot.Helm)] = goal });
        return (engine ?? new SlotDiffEngine()).Diff(loadout, build).Slots.Single();
    }

    private static GearItem Helm(IEnumerable<GearAffix> affixes, uint? uniqueHash = null)
        => ProgressionTestFactory.Item(GearSlot.Helm, affixes, uniqueHash);

    // --- Empty TargetAffixIds ---------------------------------------------------------------

    [Fact]
    public void NOfM_EmptyTargetSet_TriviallyMeetsGoal_WithGearEquipped()
    {
        // Math.Min(RequiredAffixCount, 0) == 0, so matched.Count (0) >= 0 is always true.
        // Documents the trivial-meet behavior when a slot goal has no target affixes.
        var item = Helm([ProgressionTestFactory.Affix(A)]);
        var goal = new SlotGoal { TargetAffixIds = [], Threshold = MeetsGoalThreshold.NOf(3) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
        diff.MatchedAffixCount.ShouldBe(0);
        diff.MissingAffixIds.ShouldBeEmpty();
    }

    [Fact]
    public void NOfM_EmptyTargetSet_NoGear_StillNeedsRule()
    {
        // meets requires item is not null first, so an empty target set does NOT bypass the
        // "must have gear equipped" gate.
        var goal = new SlotGoal { TargetAffixIds = [], Threshold = MeetsGoalThreshold.NOf(3) };

        var diff = DiffHelm(null, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.Notes.ShouldContain("no gear equipped");
    }

    [Fact]
    public void ExactMatch_EmptyTargetSet_TriviallyMeetsGoal()
    {
        // missing.Count == 0 trivially when there are no targets to miss.
        var item = Helm([ProgressionTestFactory.Affix(A)]);
        var goal = new SlotGoal { TargetAffixIds = [], Threshold = MeetsGoalThreshold.Exact };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    // --- RequiredAffixCount boundaries -------------------------------------------------------

    [Fact]
    public void NOfM_RequiredAffixCountZero_TriviallyMeetsGoal_EvenWithNoMatches()
    {
        var item = Helm([ProgressionTestFactory.Affix(C)]); // C not a target
        var goal = new SlotGoal { TargetAffixIds = [A, B], Threshold = MeetsGoalThreshold.NOf(0) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
        diff.MatchedAffixCount.ShouldBe(0);
    }

    [Fact]
    public void NOfM_RequiredAffixCountNegative_TriviallyMeetsGoal()
    {
        // Math.Min(-1, targets.Count) == -1; matched.Count (>= 0) >= -1 is always true.
        // Confirms current (possibly unintended) behavior: a negative threshold acts like zero.
        var item = Helm([ProgressionTestFactory.Affix(C)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B], Threshold = MeetsGoalThreshold.NOf(-1) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    [Fact]
    public void NOfM_RequiredAffixCountOne_JustAboveZero_RequiresSingleMatch()
    {
        var itemNoMatch = Helm([ProgressionTestFactory.Affix(C)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B], Threshold = MeetsGoalThreshold.NOf(1) };

        DiffHelm(itemNoMatch, goal).Status.ShouldBe(SlotDiffStatus.NeedsRule);

        var itemOneMatch = Helm([ProgressionTestFactory.Affix(A)]);
        DiffHelm(itemOneMatch, goal).Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    [Fact]
    public void NOfM_RequiredAffixCountEqualsTargetCount_ExactBoundary()
    {
        // Just-below-max within the cap: RequiredAffixCount == targets.Count exactly.
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B], Threshold = MeetsGoalThreshold.NOf(2) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
        diff.MatchedAffixCount.ShouldBe(2);
    }

    // --- RequiredGreaterAffixCount boundaries ------------------------------------------------

    [Fact]
    public void GreaterAffixCount_RequiredZero_DoesNotGateOnGA()
    {
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)]); // no GA at all
        var goal = new SlotGoal { TargetAffixIds = [A, B], Threshold = MeetsGoalThreshold.WithGreaterAffixes(2, 0) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
        diff.MatchedGreaterAffixCount.ShouldBe(0);
    }

    [Fact]
    public void GreaterAffixCount_RequiredNegative_TriviallyMeetsGaGate()
    {
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B], Threshold = MeetsGoalThreshold.WithGreaterAffixes(2, -1) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    [Fact]
    public void GreaterAffixCount_RequiredExceedsMatchedCount_NeedsRule()
    {
        // Above-max: requesting more GA than there are matched targets at all.
        var item = Helm([ProgressionTestFactory.Affix(A, greater: true), ProgressionTestFactory.Affix(B, greater: true)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B], Threshold = MeetsGoalThreshold.WithGreaterAffixes(2, 3) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
    }

    [Fact]
    public void GreaterAffixCount_AffixCountMetButGaCountNot_NeedsRule()
    {
        // Affix threshold alone would pass; GA sub-threshold must independently gate it.
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)]); // 0 GA
        var goal = new SlotGoal { TargetAffixIds = [A, B], Threshold = MeetsGoalThreshold.WithGreaterAffixes(2, 1) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.MatchedAffixCount.ShouldBe(2);
        diff.MatchedGreaterAffixCount.ShouldBe(0);
    }

    // --- Duplicate affix hash GA-fold (order-dependent OR) -----------------------------------

    [Fact]
    public void DuplicateHash_GreaterFirst_ThenNonGreater_StaysGreater()
    {
        var item = Helm([
            ProgressionTestFactory.Affix(A, greater: true),
            ProgressionTestFactory.Affix(A, greater: false),
        ]);
        var goal = new SlotGoal { TargetAffixIds = [A], Threshold = MeetsGoalThreshold.WithGreaterAffixes(1, 1) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
        diff.MatchedGreaterAffixCount.ShouldBe(1);
    }

    [Fact]
    public void DuplicateHash_NonGreaterFirst_ThenGreater_FoldsToGreater()
    {
        // OR-fold must not let a later "true" be overwritten by the earlier "false", and must not
        // let the first-seen "false" win either — the fold is present[hash] = old || new.
        var item = Helm([
            ProgressionTestFactory.Affix(A, greater: false),
            ProgressionTestFactory.Affix(A, greater: true),
        ]);
        var goal = new SlotGoal { TargetAffixIds = [A], Threshold = MeetsGoalThreshold.WithGreaterAffixes(1, 1) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
        diff.MatchedGreaterAffixCount.ShouldBe(1);
    }

    [Fact]
    public void DuplicateHash_BothNonGreater_StaysNonGreater()
    {
        var item = Helm([
            ProgressionTestFactory.Affix(A, greater: false),
            ProgressionTestFactory.Affix(A, greater: false),
        ]);
        var goal = new SlotGoal { TargetAffixIds = [A], Threshold = MeetsGoalThreshold.WithGreaterAffixes(1, 1) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.MatchedGreaterAffixCount.ShouldBe(0);
    }

    [Fact]
    public void DuplicateHash_InTargetList_DoesNotDoubleCountMatch()
    {
        // A D4 item cannot roll the same affix twice, so a duplicate hash in TargetAffixIds is one
        // distinct target. The engine dedupes targets before counting: matching the single present
        // affix A counts as 1 hit (not 2), and only distinct targets remain unmatched.
        var item = Helm([ProgressionTestFactory.Affix(A)]);
        var goal = new SlotGoal { TargetAffixIds = [A, A, B], Threshold = MeetsGoalThreshold.NOf(2) };

        var diff = DiffHelm(item, goal);

        diff.MatchedAffixCount.ShouldBe(1);
        diff.TargetAffixIds.ShouldBe([A, B]);
        // Only A present out of {A, B} → 1 of 2 distinct targets → NOf(2) not met.
        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
    }

    [Fact]
    public void DuplicateTargetAffix_RequiredCountReflectsDistinctMatches_EndToEnd()
    {
        // Regression: a build helm listing "Maximum Life" twice (B here) plus a piece equipped with 2
        // of the distinct targets must yield a rule requiring 3 (matched 2 + 1), not 4. Before the fix,
        // the duplicate double-counted the match, inflating requiredCount to 4.
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B, C, 0x1004u, 0x1005u, B] };

        var diff = DiffHelm(item, goal);
        diff.MatchedAffixCount.ShouldBe(2);

        var resolver = new NameResolver(new FilterDataService());
        var generator = new ProgressionFilterGenerator(resolver, new WeaponRoleMap(resolver));
        var result = generator.Generate(new SlotDiffResult { Slots = [diff] });

        // The equipped piece has 2 matched, none greater, so a "Helm (Greater)" companion is also emitted;
        // this regression is about the base affix-count rule, so select it by name.
        var helmRule = result.Ruleset.Rules.Single(r => r.Name == "Helm");
        helmRule.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(3);
    }

    [Fact]
    public void DuplicateTargetAffix_ThatIsAlsoGreaterAffix_GreaterAffixCountNotInflated()
    {
        // Target-side duplicate of a hash the item rolled as a greater affix must still count as
        // exactly one GA match — matchedGa iterates the already-deduped `matched` list, so a
        // repeated target hash cannot double-count the GA hit either.
        var item = Helm([ProgressionTestFactory.Affix(A, greater: true), ProgressionTestFactory.Affix(B)]);
        var goal = new SlotGoal { TargetAffixIds = [A, A, B], Threshold = MeetsGoalThreshold.WithGreaterAffixes(2, 1) };

        var diff = DiffHelm(item, goal);

        diff.MatchedAffixCount.ShouldBe(2);
        diff.MatchedGreaterAffixCount.ShouldBe(1);
        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    [Fact]
    public void AllDuplicateTargetAffixes_DedupeToSingleTarget_ClampsRequiredCountToOne()
    {
        // A target list that is entirely one hash repeated dedupes to a single distinct target, so
        // the NOfM Math.Min(RequiredAffixCount, targets.Count) clamp collapses to 1 regardless of
        // the configured RequiredAffixCount — a single distinct target can never require 2+ matches.
        var item = Helm([ProgressionTestFactory.Affix(A)]);
        var goal = new SlotGoal { TargetAffixIds = [A, A, A], Threshold = MeetsGoalThreshold.NOf(3) };

        var diff = DiffHelm(item, goal);

        diff.TargetAffixIds.ShouldBe([A]);
        diff.MatchedAffixCount.ShouldBe(1);
        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    [Fact]
    public void ExactMatch_DuplicateTargetAffix_MissingListIsDeduped()
    {
        // ExactMatch's missing.Count == 0 check is unaffected by duplicates either way, but
        // MissingAffixIds itself must reflect the deduped target set — one entry per distinct
        // missing hash, not one per duplicate occurrence in the goal's raw target list.
        var item = Helm([ProgressionTestFactory.Affix(A)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B, B], Threshold = MeetsGoalThreshold.Exact };

        var diff = DiffHelm(item, goal);

        diff.MissingAffixIds.ShouldBe([B]);
        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
    }

    // --- Unique + affix interaction / note independence --------------------------------------

    [Fact]
    public void UniqueMatches_ButAffixThresholdUnmet_NeedsRule_NoWrongUniqueNote()
    {
        var item = Helm([ProgressionTestFactory.Affix(A)], uniqueHash: UniqueU);
        var goal = new SlotGoal { TargetAffixIds = [A, B, C], TargetUnique = UniqueU, Threshold = MeetsGoalThreshold.Exact };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.UniqueSatisfied.ShouldBeTrue();
        diff.Notes.ShouldNotContain("wrong or missing unique");
    }

    [Fact]
    public void NoUniqueRequired_ItemHasUnrelatedUnique_EvaluatesOnAffixesOnly()
    {
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)], uniqueHash: UniqueU);
        var goal = new SlotGoal { TargetAffixIds = [A, B], TargetUnique = null, Threshold = MeetsGoalThreshold.NOf(2) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
        diff.UniqueSatisfied.ShouldBeTrue();
    }

    [Fact]
    public void UniqueOnlyGoal_EmptyTargetAffixIds_MeetsGoalWhenUniqueMatches()
    {
        // A unique-gated slot with no affix targets at all — trivial NOfM(0-of-0) plus unique gate.
        var item = Helm([], uniqueHash: UniqueU);
        var goal = new SlotGoal { TargetAffixIds = [], TargetUnique = UniqueU, Threshold = MeetsGoalThreshold.NOf(3) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    // --- Item with no affixes at all ----------------------------------------------------------

    [Fact]
    public void ItemWithNoAffixes_AllTargetsMissing()
    {
        var item = Helm([]);
        var goal = new SlotGoal { TargetAffixIds = [A, B], Threshold = MeetsGoalThreshold.NOf(1) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.MissingAffixIds.ShouldBe([A, B]);
        diff.MatchedAffixCount.ShouldBe(0);
    }

    [Fact]
    public void ItemWithOnlyUnresolvedAffixes_TreatedAsNoMatches()
    {
        var item = Helm([ProgressionTestFactory.UnresolvedAffix(), ProgressionTestFactory.UnresolvedAffix()]);
        var goal = new SlotGoal { TargetAffixIds = [A], Threshold = MeetsGoalThreshold.NOf(1) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.MissingAffixIds.ShouldBe([A]);
    }

    // --- Engine default threshold (no SlotGoal.Threshold override) ---------------------------

    [Fact]
    public void EngineDefaultThreshold_UsedWhenGoalOmitsThreshold()
    {
        // Default engine ctor uses NOf(3). Two matches out of a 3-target goal should NOT meet.
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B, C] }; // Threshold left null

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.MatchedAffixCount.ShouldBe(2);
    }

    [Fact]
    public void EngineDefaultThreshold_MetWhenAllThreeTargetsMatch()
    {
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B), ProgressionTestFactory.Affix(C)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B, C] };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    [Fact]
    public void CustomEngineDefaultThreshold_OverridesBuiltInDefault()
    {
        var item = Helm([ProgressionTestFactory.Affix(A)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B, C] }; // Threshold left null
        var engine = new SlotDiffEngine(MeetsGoalThreshold.NOf(1));

        var diff = DiffHelm(item, goal, engine);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    // --- Rule ceiling exactness ----------------------------------------------------------------

    [Theory]
    [InlineData(0, true, 25)]
    [InlineData(1, true, 24)]
    [InlineData(24, true, 1)]
    [InlineData(25, true, 0)]
    [InlineData(26, false, -1)]
    public void WithinRuleBudget_ExactCeilingBoundaries(int ruleCount, bool expectedWithinBudget, int expectedRemaining)
    {
        var diffs = Enumerable.Range(0, ruleCount)
            .Select(i => new SlotDiff { Slot = new SlotKey(GearSlot.Ring, i), Status = SlotDiffStatus.NeedsRule })
            .ToList();

        var result = new SlotDiffResult { Slots = diffs };

        result.RuleCount.ShouldBe(ruleCount);
        result.WithinRuleBudget.ShouldBe(expectedWithinBudget);
        result.RemainingRuleBudget.ShouldBe(expectedRemaining);
    }

    // --- Empty loadout + empty goal build -------------------------------------------------------

    [Fact]
    public void EmptyLoadout_EmptyGoalBuild_ProducesZeroSlotsAndFullBudget()
    {
        var loadout = new EquippedLoadout(new Dictionary<SlotKey, GearItem>());
        var build = new GoalBuild(new Dictionary<SlotKey, SlotGoal>());

        var result = new SlotDiffEngine().Diff(loadout, build);

        result.Slots.ShouldBeEmpty();
        result.RuleCount.ShouldBe(0);
        result.WithinRuleBudget.ShouldBeTrue();
        result.RemainingRuleBudget.ShouldBe(25);
    }

    // --- SlotKey identity / formatting -----------------------------------------------------------

    [Fact]
    public void SlotKey_ToString_OrdinalZero_OmitsSuffix()
    {
        new SlotKey(GearSlot.Ring, 0).ToString().ShouldBe("Ring");
    }

    [Fact]
    public void SlotKey_ToString_OrdinalOne_AppendsOneBasedSuffix()
    {
        new SlotKey(GearSlot.Ring, 1).ToString().ShouldBe("Ring#2");
    }

    [Fact]
    public void SlotKey_For_FactoryMatchesConstructor()
    {
        SlotKey.For(GearSlot.Weapon, 1).ShouldBe(new SlotKey(GearSlot.Weapon, 1));
    }

    [Fact]
    public void SlotKey_NegativeOrdinal_DoesNotThrow_FormatsPerCurrentRule()
    {
        // Ordinal is a plain int with no validation; document rather than assume a guard exists.
        var key = new SlotKey(GearSlot.Ring, -1);

        key.Ordinal.ShouldBe(-1);
        key.ToString().ShouldBe("Ring#0");
    }

    // --- SlotKey.Role equality / formatting (class-aware weapon slot role feature) -----

    [Fact]
    public void SlotKey_DifferentRole_NotEqual()
    {
        new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand)
            .ShouldNotBe(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Slicing));
    }

    [Fact]
    public void SlotKey_NoneRole_NotEqualToMainhandRole()
    {
        new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.None)
            .ShouldNotBe(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand));
    }

    [Fact]
    public void SlotKey_SameSlotOrdinalRole_AreEqual()
    {
        new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand)
            .ShouldBe(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand));
    }

    [Fact]
    public void SlotKey_ToString_WithRole_FormatsAsRoleName()
    {
        new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Slicing).ToString().ShouldBe("Slicing");
        new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.TwoHand).ToString().ShouldBe("TwoHand");
    }

    // --- SlotDiffEngine.Diff ordering with mixed role weapon keys ----------------------

    [Fact]
    public void Diff_MixedRoleWeaponKeys_OrderedByRoleEnum()
    {
        // SlotDiffEngine.Diff tiebreaks on Role (enum order) after Slot and Ordinal — None(0) sorts
        // before Mainhand(1) before Offhand(2).
        var loadout = new EquippedLoadout(new Dictionary<SlotKey, GearItem>
        {
            [new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Offhand)] = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(A)]),
            [new SlotKey(GearSlot.Weapon, 0)] = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(A)]),
            [new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand)] = ProgressionTestFactory.Item(GearSlot.Weapon, [ProgressionTestFactory.Affix(A)]),
        });
        var build = new GoalBuild(new Dictionary<SlotKey, SlotGoal>());

        var result = new SlotDiffEngine().Diff(loadout, build);

        result.Slots.Select(s => s.Slot.Role)
            .ShouldBe([WeaponSlotRole.None, WeaponSlotRole.Mainhand, WeaponSlotRole.Offhand]);
        result.Slots.ShouldAllBe(s => s.Status == SlotDiffStatus.NoGoal);
    }
}

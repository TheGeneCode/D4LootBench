namespace D4LootBench.Core.Tests.Progression;

using D4LootBench.Core.Gear;
using D4LootBench.Core.Progression;
using Shouldly;
using Xunit;

public class SlotDiffEngineTests
{
    private const uint A = 0x1001;
    private const uint B = 0x1002;
    private const uint C = 0x1003;
    private const uint D = 0x1004;
    private const uint UniqueU = 0x2001;
    private const uint UniqueX = 0x2002;

    // Diffs a single Helm slot: builds a one-item loadout and a one-goal build, runs the engine,
    // and returns that slot's diff.
    private static SlotDiff DiffHelm(GearItem? item, SlotGoal goal)
    {
        var items = new Dictionary<SlotKey, GearItem>();
        if (item is not null)
        {
            items[new SlotKey(GearSlot.Helm)] = item;
        }

        var loadout = new EquippedLoadout(items);
        var build = new GoalBuild(new Dictionary<SlotKey, SlotGoal> { [new SlotKey(GearSlot.Helm)] = goal });
        return new SlotDiffEngine().Diff(loadout, build).Slots.Single();
    }

    private static GearItem Helm(IEnumerable<GearAffix> affixes, uint? uniqueHash = null)
        => ProgressionTestFactory.Item(GearSlot.Helm, affixes, uniqueHash);

    [Fact]
    public void MeetsGoal_ExactMatch_AllTargetsPresent_NoRule()
    {
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B), ProgressionTestFactory.Affix(C)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B, C], Threshold = MeetsGoalThreshold.Exact };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    [Fact]
    public void Exact_OneTargetMissing_NeedsRule()
    {
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B, C], Threshold = MeetsGoalThreshold.Exact };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.MissingAffixIds.ShouldBe([C]);
    }

    [Fact]
    public void NOfM_MeetsThreshold()
    {
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B, C, D], Threshold = MeetsGoalThreshold.NOf(2) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
        diff.MatchedAffixCount.ShouldBe(2);
    }

    [Fact]
    public void NOfM_RequiredExceedsTargetCount_CapsAtTargetSize()
    {
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B], Threshold = MeetsGoalThreshold.NOf(5) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    [Fact]
    public void NOfM_BelowThreshold_NeedsRule()
    {
        var item = Helm([ProgressionTestFactory.Affix(A)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B, C], Threshold = MeetsGoalThreshold.NOf(2) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.MissingAffixIds.ShouldBe([B, C]);
    }

    [Fact]
    public void GreaterAffixCount_MetWhenEnoughGA()
    {
        var item = Helm([
            ProgressionTestFactory.Affix(A, greater: true),
            ProgressionTestFactory.Affix(B, greater: true),
            ProgressionTestFactory.Affix(C),
        ]);
        var goal = new SlotGoal { TargetAffixIds = [A, B, C], Threshold = MeetsGoalThreshold.WithGreaterAffixes(3, 2) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
        diff.MatchedGreaterAffixCount.ShouldBe(2);
    }

    [Fact]
    public void GreaterAffixCount_FailsWhenTooFewGA()
    {
        var item = Helm([
            ProgressionTestFactory.Affix(A, greater: true),
            ProgressionTestFactory.Affix(B),
            ProgressionTestFactory.Affix(C),
        ]);
        var goal = new SlotGoal { TargetAffixIds = [A, B, C], Threshold = MeetsGoalThreshold.WithGreaterAffixes(3, 2) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
    }

    [Fact]
    public void UniqueGate_ItemIsTargetUnique_AndAffixesMet_MeetsGoal()
    {
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)], uniqueHash: UniqueU);
        var goal = new SlotGoal { TargetAffixIds = [A, B], TargetUnique = UniqueU, Threshold = MeetsGoalThreshold.NOf(2) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
        diff.UniqueSatisfied.ShouldBeTrue();
    }

    [Fact]
    public void UniqueGate_WrongUnique_NeedsRule_EvenIfAffixesMet()
    {
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)], uniqueHash: UniqueX);
        var goal = new SlotGoal { TargetAffixIds = [A, B], TargetUnique = UniqueU, Threshold = MeetsGoalThreshold.NOf(2) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.UniqueSatisfied.ShouldBeFalse();
        diff.Notes.ShouldContain("wrong or missing unique");
    }

    [Fact]
    public void UniqueGate_NoUniqueOnItem_NeedsRule()
    {
        var item = Helm([ProgressionTestFactory.Affix(A), ProgressionTestFactory.Affix(B)]);
        var goal = new SlotGoal { TargetAffixIds = [A, B], TargetUnique = UniqueU, Threshold = MeetsGoalThreshold.NOf(2) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.UniqueSatisfied.ShouldBeFalse();
    }

    [Fact]
    public void NoGear_GoalExists_NeedsRule()
    {
        var goal = new SlotGoal { TargetAffixIds = [A, B], Threshold = MeetsGoalThreshold.NOf(2) };

        var diff = DiffHelm(null, goal);

        diff.Status.ShouldBe(SlotDiffStatus.NeedsRule);
        diff.EquippedItem.ShouldBeNull();
        diff.Notes.ShouldContain("no gear equipped");
    }

    [Fact]
    public void GearPresent_NoGoal_ReportedAsNoGoal()
    {
        var item = ProgressionTestFactory.Item(GearSlot.Helm, [ProgressionTestFactory.Affix(A)]);
        var loadout = new EquippedLoadout(new Dictionary<SlotKey, GearItem> { [new SlotKey(GearSlot.Helm)] = item });
        var build = new GoalBuild(new Dictionary<SlotKey, SlotGoal>());

        var result = new SlotDiffEngine().Diff(loadout, build);

        var diff = result.Slots.Single();
        diff.Status.ShouldBe(SlotDiffStatus.NoGoal);
        result.SlotsNeedingRules.ShouldBeEmpty();
    }

    [Fact]
    public void UnresolvedAffix_IgnoredInMatch()
    {
        var item = Helm([ProgressionTestFactory.UnresolvedAffix(), ProgressionTestFactory.Affix(A)]);
        var goal = new SlotGoal { TargetAffixIds = [A], Threshold = MeetsGoalThreshold.NOf(1) };

        var diff = DiffHelm(item, goal);

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    [Fact]
    public void GoalBuild_OrdinalFallsBackToSlotZero_Diffed()
    {
        var ring1 = ProgressionTestFactory.Item(GearSlot.Ring, [ProgressionTestFactory.Affix(A)]);
        var loadout = new EquippedLoadout(new Dictionary<SlotKey, GearItem> { [new SlotKey(GearSlot.Ring, 1)] = ring1 });
        var goal = new SlotGoal { TargetAffixIds = [A], Threshold = MeetsGoalThreshold.NOf(1) };
        var build = new GoalBuild(new Dictionary<SlotKey, SlotGoal> { [new SlotKey(GearSlot.Ring, 0)] = goal });

        var diff = new SlotDiffEngine().Diff(loadout, build).Slots
            .Single(s => s.Slot == new SlotKey(GearSlot.Ring, 1));

        diff.Status.ShouldBe(SlotDiffStatus.MeetsGoal);
    }

    [Fact]
    public void RuleCount_And_Budget()
    {
        var goal = new SlotGoal { TargetAffixIds = [A], Threshold = MeetsGoalThreshold.NOf(1) };
        var goals = new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Helm)] = goal,
            [new SlotKey(GearSlot.Gloves)] = goal,
            [new SlotKey(GearSlot.Boots)] = goal,
        };
        var loadout = new EquippedLoadout(new Dictionary<SlotKey, GearItem>());

        var result = new SlotDiffEngine().Diff(loadout, new GoalBuild(goals));

        result.RuleCount.ShouldBe(3);
        result.WithinRuleBudget.ShouldBeTrue();
        result.RemainingRuleBudget.ShouldBe(22);
    }

    [Fact]
    public void Budget_ExceededReportsFalse()
    {
        var diffs = Enumerable.Range(0, 26)
            .Select(i => new SlotDiff { Slot = new SlotKey(GearSlot.Ring, i), Status = SlotDiffStatus.NeedsRule })
            .ToList();

        var result = new SlotDiffResult { Slots = diffs };

        result.RuleCount.ShouldBe(26);
        result.WithinRuleBudget.ShouldBeFalse();
        result.RemainingRuleBudget.ShouldBe(-1);
    }

    [Fact]
    public void Diff_OutputIsDeterministicallyOrdered()
    {
        var goal = new SlotGoal { TargetAffixIds = [A], Threshold = MeetsGoalThreshold.NOf(1) };
        var goals = new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Ring, 1)] = goal,
            [new SlotKey(GearSlot.Helm)] = goal,
            [new SlotKey(GearSlot.Ring, 0)] = goal,
            [new SlotKey(GearSlot.Boots)] = goal,
        };
        var loadout = new EquippedLoadout(new Dictionary<SlotKey, GearItem>());

        var result = new SlotDiffEngine().Diff(loadout, new GoalBuild(goals));

        result.Slots.Select(s => s.Slot).ShouldBe([
            new SlotKey(GearSlot.Helm),
            new SlotKey(GearSlot.Boots),
            new SlotKey(GearSlot.Ring, 0),
            new SlotKey(GearSlot.Ring, 1),
        ]);
    }
}

namespace D4LootBench.Core.Tests.Progression;

using D4LootBench.Core.Gear;
using D4LootBench.Core.Progression;
using Shouldly;
using Xunit;

public class GoalBuildTests
{
    [Fact]
    public void OrdinalFallsBackToSlotZero()
    {
        var g0 = new SlotGoal { TargetAffixIds = [1, 2] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal> { [new SlotKey(GearSlot.Ring, 0)] = g0 });

        goal.Lookup(new SlotKey(GearSlot.Ring, 1)).ShouldBe(g0);
    }

    [Fact]
    public void ExactOrdinalOverridesFallback()
    {
        var g0 = new SlotGoal { TargetAffixIds = [1] };
        var g1 = new SlotGoal { TargetAffixIds = [2] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Ring, 0)] = g0,
            [new SlotKey(GearSlot.Ring, 1)] = g1,
        });

        goal.Lookup(new SlotKey(GearSlot.Ring, 1)).ShouldBe(g1);
    }

    [Fact]
    public void NoGoalForSlot_ReturnsNull()
    {
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>());

        goal.Lookup(new SlotKey(GearSlot.Helm)).ShouldBeNull();
    }
}

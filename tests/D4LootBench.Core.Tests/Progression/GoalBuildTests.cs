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
    public void Lookup_RoleKeyExactMatch()
    {
        var slicing = new SlotGoal { TargetAffixIds = [1, 2] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Slicing)] = slicing,
        });

        goal.Lookup(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Slicing)).ShouldBe(slicing);
    }

    [Fact]
    public void Lookup_RoleOrdinalStripFallback()
    {
        // An ordinal>0 role slot falls back to the (Slot, 0, Role) default.
        var mainhand = new SlotGoal { TargetAffixIds = [1] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand)] = mainhand,
        });

        goal.Lookup(new SlotKey(GearSlot.Weapon, 1, WeaponSlotRole.Mainhand)).ShouldBe(mainhand);
    }

    [Fact]
    public void Lookup_DifferentRole_DoesNotFallBack()
    {
        // Role is part of identity — a Slicing goal must NOT resolve for a Mainhand lookup.
        var slicing = new SlotGoal { TargetAffixIds = [1] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Slicing)] = slicing,
        });

        goal.Lookup(new SlotKey(GearSlot.Weapon, 0, WeaponSlotRole.Mainhand)).ShouldBeNull();
    }

    [Fact]
    public void Lookup_RingOrdinalFallbackStillWorks()
    {
        var g0 = new SlotGoal { TargetAffixIds = [1] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal> { [new SlotKey(GearSlot.Ring)] = g0 });

        goal.Lookup(new SlotKey(GearSlot.Ring, 1)).ShouldBe(g0);
    }

    [Fact]
    public void NoGoalForSlot_ReturnsNull()
    {
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>());

        goal.Lookup(new SlotKey(GearSlot.Helm)).ShouldBeNull();
    }

    [Fact]
    public void Lookup_NullRoleSecondWeapon_FallsBackToSlotDefault()
    {
        // Two weapons with no resolved role land at (Weapon,0,None) and (Weapon,1,None) — the second
        // has Ordinal!=0 and the same Role, so it resolves to the (Slot,0,Role) ordinal-strip default.
        var slotDefault = new SlotGoal { TargetAffixIds = [1] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal> { [new SlotKey(GearSlot.Weapon)] = slotDefault });

        goal.Lookup(new SlotKey(GearSlot.Weapon, 1)).ShouldBe(slotDefault);
    }
}

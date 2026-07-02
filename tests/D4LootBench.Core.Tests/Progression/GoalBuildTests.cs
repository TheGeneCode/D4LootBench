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
    public void Lookup_ConcreteWeaponResolvesToFamilyGoal()
    {
        var family = new SlotGoal { TargetAffixIds = [1, 2] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal> { [new SlotKey(GearSlot.Weapon)] = family });

        goal.Lookup(new SlotKey(GearSlot.Weapon, 0, "Two-Handed Axe")).ShouldBe(family);
    }

    [Fact]
    public void Lookup_ExactWeaponTypeGoalBeatsFamily()
    {
        var family = new SlotGoal { TargetAffixIds = [1] };
        var polearm = new SlotGoal { TargetAffixIds = [2] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Weapon)] = family,
            [new SlotKey(GearSlot.Weapon, 0, "Polearm")] = polearm,
        });

        goal.Lookup(new SlotKey(GearSlot.Weapon, 0, "Polearm")).ShouldBe(polearm);
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
    public void Lookup_NullTypeSecondWeapon_FallsBackToSlotDefault()
    {
        // Two weapons with unresolved OCR types land at (Weapon,0,null) and (Weapon,1,null) — the
        // second one has Ordinal!=0 and ItemType==null, so it skips the family-strip tier entirely
        // (guarded on key.ItemType is not null) and lands on the (Slot,0) ordinal-strip tier instead.
        var slotDefault = new SlotGoal { TargetAffixIds = [1] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal> { [new SlotKey(GearSlot.Weapon)] = slotDefault });

        goal.Lookup(new SlotKey(GearSlot.Weapon, 1)).ShouldBe(slotDefault);
    }

    [Fact]
    public void Lookup_CaseMismatchedItemType_NoFamilyDefault_ReturnsNull()
    {
        // SlotKey equality is case-sensitive (plain string ==). A goal authored for "Sword" is
        // invisible to gear whose OCR-read type differs only in case, with no family default to
        // catch it — this is the case-sensitivity risk flagged for this feature.
        var perType = new SlotGoal { TargetAffixIds = [1] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal> { [new SlotKey(GearSlot.Weapon, 0, "Sword")] = perType });

        goal.Lookup(new SlotKey(GearSlot.Weapon, 0, "sword")).ShouldBeNull();
    }

    [Fact]
    public void Lookup_CaseMismatchedItemType_FamilyDefaultPresent_SilentlyFallsBackToFamily_NotPerTypeGoal()
    {
        // When a family default IS authored, a case-mismatched exact key doesn't fail outright — but
        // it also does NOT reach the per-type override; it silently resolves to the (weaker/different)
        // family goal instead. Documents that case mismatches degrade specificity rather than erroring.
        var family = new SlotGoal { TargetAffixIds = [9] };
        var perType = new SlotGoal { TargetAffixIds = [1] };
        var goal = new GoalBuild(new Dictionary<SlotKey, SlotGoal>
        {
            [new SlotKey(GearSlot.Weapon)] = family,
            [new SlotKey(GearSlot.Weapon, 0, "Sword")] = perType,
        });

        goal.Lookup(new SlotKey(GearSlot.Weapon, 0, "sword")).ShouldBe(family);
    }
}

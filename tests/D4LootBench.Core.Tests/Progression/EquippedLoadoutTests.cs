namespace D4LootBench.Core.Tests.Progression;

using D4LootBench.Core.Gear;
using D4LootBench.Core.Progression;
using Shouldly;
using Xunit;

public class EquippedLoadoutTests
{
    [Fact]
    public void FromItems_AssignsOrdinals()
    {
        var ring0 = ProgressionTestFactory.Item(GearSlot.Ring, [ProgressionTestFactory.Affix(1)]);
        var ring1 = ProgressionTestFactory.Item(GearSlot.Ring, [ProgressionTestFactory.Affix(2)]);

        var loadout = EquippedLoadout.FromItems([ring0, ring1]);

        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Ring, 0));
        loadout.Items.ShouldContainKey(new SlotKey(GearSlot.Ring, 1));
        loadout[new SlotKey(GearSlot.Ring, 0)].ShouldBe(ring0);
        loadout[new SlotKey(GearSlot.Ring, 1)].ShouldBe(ring1);
    }

    [Fact]
    public void FromItems_SkipsUnknownSlot()
    {
        var unknown = ProgressionTestFactory.Item(GearSlot.Unknown, [ProgressionTestFactory.Affix(1)]);

        var loadout = EquippedLoadout.FromItems([unknown]);

        loadout.Items.ShouldBeEmpty();
    }

    [Fact]
    public void Indexer_ReturnsNullForEmptySlot()
    {
        var loadout = EquippedLoadout.FromItems([]);

        loadout[new SlotKey(GearSlot.Helm)].ShouldBeNull();
    }
}

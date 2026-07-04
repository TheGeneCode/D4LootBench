using D4LootBench.Core.Gear;
using Shouldly;

namespace D4LootBench.Core.Tests.Gear;

public sealed class ItemAffixLimitsTests
{
    [Fact]
    public void ItemAffixLimits_Magic_CapsAt3()
    {
        ItemAffixLimits.RollableAffixCap(ItemRarity.Magic).ShouldBe(3);
    }

    [Theory]
    [InlineData(ItemRarity.Rare)]
    [InlineData(ItemRarity.Legendary)]
    [InlineData(ItemRarity.Unique)]
    [InlineData(ItemRarity.Mythic)]
    [InlineData(ItemRarity.Common)]
    [InlineData(ItemRarity.Unknown)]
    public void ItemAffixLimits_NonMagic_CapsAt4(ItemRarity rarity)
    {
        ItemAffixLimits.RollableAffixCap(rarity).ShouldBe(4);
    }

    [Fact]
    public void ItemAffixLimits_MaxGa_Is3()
    {
        ItemAffixLimits.MaxGreaterAffixCount.ShouldBe(3);
    }
}

using D4LootBench.Core.Gear;
using Shouldly;

namespace D4LootBench.Core.Tests.Gear;

public sealed class GearReviewSessionTests
{
    private static GearParseResult Result(GearItem item, GearParseConfidence confidence = GearParseConfidence.High)
        => new() { Item = item, Confidence = confidence };

    [Fact]
    public void Review_ToggleGreaterAffix_SetsFlag()
    {
        var item = new GearItem
        {
            Slot = GearSlot.Helm,
            Affixes = [new GearAffix { RawText = "+112 Maximum Life", ResolvedName = "Maximum Life", AffixHash = 0x1234 }],
        };
        var session = new GearReviewSession([Result(item)]);

        session.ToggleGreaterAffix(0, 0);

        session.Build()[0].Affixes[0].IsGreaterAffix.ShouldBeTrue();
    }

    [Fact]
    public void Review_SetSlot_CorrectsUnknown()
    {
        var item = new GearItem { Slot = GearSlot.Unknown };
        var session = new GearReviewSession([Result(item, GearParseConfidence.Low)]);

        session.SetSlot(0, GearSlot.Amulet);

        session.Build()[0].Slot.ShouldBe(GearSlot.Amulet);
    }

    [Fact]
    public void Review_Build_FoldsCorrections()
    {
        var itemA = new GearItem
        {
            Slot = GearSlot.Unknown,
            ItemPower = 800,
            Affixes = [new GearAffix { RawText = "+30 Dexterity", AffixHash = 0x1 }],
        };
        var itemB = new GearItem { Slot = GearSlot.Ring, ItemPower = 750 };
        var session = new GearReviewSession([Result(itemA), Result(itemB)]);

        session.SetSlot(0, GearSlot.Weapon);
        session.ToggleGreaterAffix(0, 0);
        session.Items[1].ItemPower = 925;

        var built = session.Build();

        built.Count.ShouldBe(2);
        built[0].Slot.ShouldBe(GearSlot.Weapon);
        built[0].Affixes[0].IsGreaterAffix.ShouldBeTrue();
        built[1].ItemPower.ShouldBe(925);
    }

    [Fact]
    public void Build_UsesEditedItemTypeName()
    {
        var item = new GearItem { Slot = GearSlot.Weapon, ItemTypeName = "Sword" };
        var session = new GearReviewSession([Result(item)]);

        session.Items[0].ItemTypeName = "Polearm";

        session.Build()[0].ItemTypeName.ShouldBe("Polearm");
    }

    [Fact]
    public void Review_NeedsReview_SeededFromLowConfidence()
    {
        var session = new GearReviewSession(
        [
            Result(new GearItem { Slot = GearSlot.Helm }, GearParseConfidence.High),
            Result(new GearItem(), GearParseConfidence.Low),
        ]);

        session.Items[0].NeedsReview.ShouldBeFalse();
        session.Items[1].NeedsReview.ShouldBeTrue();
    }

    [Fact]
    public void Review_IndexOutOfRange_Throws()
    {
        var session = new GearReviewSession([Result(new GearItem())]);

        Should.Throw<ArgumentOutOfRangeException>(() => session.SetSlot(5, GearSlot.Ring));
        Should.Throw<ArgumentOutOfRangeException>(() => session.ToggleGreaterAffix(0, 3));
    }
}

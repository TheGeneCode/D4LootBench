using D4LootBench.Core.Gear;
using Shouldly;

namespace D4LootBench.Core.Tests.Gear;

/// <summary>
/// Boundary-value coverage for <see cref="GearReviewSession"/>, supplementing
/// <see cref="GearReviewSessionTests"/>. Focuses on index boundaries (first/last/negative/one-past),
/// empty-session construction, and Build() folding of mutable state back to immutable items.
/// </summary>
public sealed class GearReviewSessionBoundaryTests
{
    private static GearParseResult Result(GearItem item, GearParseConfidence confidence = GearParseConfidence.High)
        => new() { Item = item, Confidence = confidence };

    // --- Empty session ---

    [Fact]
    public void Session_EmptyParsedSequence_ProducesEmptyItemsAndBuild()
    {
        var session = new GearReviewSession([]);

        session.Items.ShouldBeEmpty();
        session.Build().ShouldBeEmpty();
    }

    [Fact]
    public void Session_EmptySession_IndexZeroThrows()
    {
        var session = new GearReviewSession([]);

        Should.Throw<ArgumentOutOfRangeException>(() => session.SetSlot(0, GearSlot.Ring));
    }

    // --- Item index boundaries: below-min, at-min, at-max, above-max ---

    [Fact]
    public void Session_ItemIndex_NegativeOne_Throws()
    {
        var session = new GearReviewSession([Result(new GearItem())]);

        Should.Throw<ArgumentOutOfRangeException>(() => session.SetSlot(-1, GearSlot.Ring));
    }

    [Fact]
    public void Session_ItemIndex_AtZero_FirstItem_Succeeds()
    {
        var session = new GearReviewSession([Result(new GearItem()), Result(new GearItem())]);

        session.SetSlot(0, GearSlot.Helm);

        session.Build()[0].Slot.ShouldBe(GearSlot.Helm);
    }

    [Fact]
    public void Session_ItemIndex_AtCountMinusOne_LastItem_Succeeds()
    {
        var session = new GearReviewSession([Result(new GearItem()), Result(new GearItem())]);

        session.SetSlot(1, GearSlot.Boots);

        session.Build()[1].Slot.ShouldBe(GearSlot.Boots);
    }

    [Fact]
    public void Session_ItemIndex_ExactlyAtCount_OnePastLast_Throws()
    {
        var session = new GearReviewSession([Result(new GearItem()), Result(new GearItem())]);

        Should.Throw<ArgumentOutOfRangeException>(() => session.SetSlot(2, GearSlot.Boots));
    }

    [Fact]
    public void Session_ItemIndex_WellAboveCount_Throws()
    {
        var session = new GearReviewSession([Result(new GearItem())]);

        Should.Throw<ArgumentOutOfRangeException>(() => session.SetSlot(int.MaxValue, GearSlot.Boots));
    }

    // --- Affix index boundaries ---

    [Fact]
    public void Session_AffixIndex_OnItemWithNoAffixes_AnyIndexThrows()
    {
        var session = new GearReviewSession([Result(new GearItem { Affixes = [] })]);

        Should.Throw<ArgumentOutOfRangeException>(() => session.ToggleGreaterAffix(0, 0));
    }

    [Fact]
    public void Session_AffixIndex_NegativeOne_Throws()
    {
        var item = new GearItem { Affixes = [new GearAffix { RawText = "+30 Dexterity" }] };
        var session = new GearReviewSession([Result(item)]);

        Should.Throw<ArgumentOutOfRangeException>(() => session.ToggleGreaterAffix(0, -1));
    }

    [Fact]
    public void Session_AffixIndex_AtLastValidIndex_Succeeds()
    {
        var item = new GearItem
        {
            Affixes =
            [
                new GearAffix { RawText = "+30 Dexterity" },
                new GearAffix { RawText = "+112 Maximum Life" },
            ],
        };
        var session = new GearReviewSession([Result(item)]);

        session.ToggleGreaterAffix(0, 1);

        session.Build()[0].Affixes[1].IsGreaterAffix.ShouldBeTrue();
        session.Build()[0].Affixes[0].IsGreaterAffix.ShouldBeFalse();
    }

    [Fact]
    public void Session_AffixIndex_OnePastLast_Throws()
    {
        var item = new GearItem { Affixes = [new GearAffix { RawText = "+30 Dexterity" }] };
        var session = new GearReviewSession([Result(item)]);

        Should.Throw<ArgumentOutOfRangeException>(() => session.ToggleGreaterAffix(0, 1));
    }

    [Fact]
    public void Session_InvalidItemIndex_ShortCircuitsBeforeAffixIndexValidated()
    {
        // itemIndex validation must happen before affixIndex is dereferenced — an invalid item
        // index combined with a wildly invalid affix index should still throw ArgumentOutOfRangeException
        // (not IndexOutOfRangeException, and not a null-reference from indexing an absent item).
        var session = new GearReviewSession([Result(new GearItem())]);

        Should.Throw<ArgumentOutOfRangeException>(() => session.ToggleGreaterAffix(99, -99));
    }

    // --- Toggle idempotency / double-toggle ---

    [Fact]
    public void Session_ToggleGreaterAffix_Twice_RevertsToFalse()
    {
        var item = new GearItem { Affixes = [new GearAffix { RawText = "+30 Dexterity" }] };
        var session = new GearReviewSession([Result(item)]);

        session.ToggleGreaterAffix(0, 0);
        session.ToggleGreaterAffix(0, 0);

        session.Build()[0].Affixes[0].IsGreaterAffix.ShouldBeFalse();
    }

    // --- Build() folds mutable state, including per-affix editable fields ---

    [Fact]
    public void Session_Build_ReflectsDirectAffixDraftMutation_NotJustToggle()
    {
        var item = new GearItem { Affixes = [new GearAffix { RawText = "+30 Zzznonexistent" }] };
        var session = new GearReviewSession([Result(item, GearParseConfidence.Low)]);

        // Simulate a human manually resolving an affix the parser could not.
        session.Items[0].Affixes[0].ResolvedName = "Dexterity";
        session.Items[0].Affixes[0].AffixHash = 0xABCD;

        var built = session.Build();

        built[0].Affixes[0].ResolvedName.ShouldBe("Dexterity");
        built[0].Affixes[0].AffixHash.ShouldBe(0xABCDu);
        built[0].Affixes[0].IsResolved.ShouldBeTrue();
    }

    [Fact]
    public void Session_Build_UnchangedItemTypeName_ReflectsSeededSourceValue()
    {
        // ItemDraft.ItemTypeName is settable (weapon-slot identity depends on it, so review can
        // correct an OCR misread) — this documents the seeded pass-through when left unedited,
        // not a read-only guarantee. See the two tests below for the editable/nullable cases.
        var item = new GearItem { Slot = GearSlot.Unknown, ItemTypeName = "Sword" };
        var session = new GearReviewSession([Result(item)]);

        session.SetSlot(0, GearSlot.Weapon);

        session.Build()[0].ItemTypeName.ShouldBe("Sword");
    }

    [Fact]
    public void Session_Build_EditedItemTypeName_OverridesSeededSourceValue_AfterSlotCorrection()
    {
        // Combined edit: correcting Slot (Unknown -> Weapon) AND ItemTypeName together, mirroring the
        // real review flow where a mis-slotted weapon also needs its concrete type corrected.
        var item = new GearItem { Slot = GearSlot.Unknown, ItemTypeName = "Sword" };
        var session = new GearReviewSession([Result(item)]);

        session.SetSlot(0, GearSlot.Weapon);
        session.Items[0].ItemTypeName = "Polearm";

        var built = session.Build()[0];
        built.Slot.ShouldBe(GearSlot.Weapon);
        built.ItemTypeName.ShouldBe("Polearm");
    }

    [Fact]
    public void Session_Build_ItemTypeNameClearedToNull_FlowsThrough()
    {
        // Clearing a bad OCR read back to null must be representable — it drops the item back onto
        // the ordinal-keyed EquippedLoadout path instead of a (possibly wrong) type-gated one.
        var item = new GearItem { Slot = GearSlot.Weapon, ItemTypeName = "Sword" };
        var session = new GearReviewSession([Result(item)]);

        session.Items[0].ItemTypeName = null;

        session.Build()[0].ItemTypeName.ShouldBeNull();
    }

    [Fact]
    public void Session_Build_CalledTwice_ReturnsIndependentSnapshotsBothReflectingLatestState()
    {
        var item = new GearItem { Slot = GearSlot.Unknown };
        var session = new GearReviewSession([Result(item)]);

        var firstBuild = session.Build();
        session.SetSlot(0, GearSlot.Amulet);
        var secondBuild = session.Build();

        firstBuild[0].Slot.ShouldBe(GearSlot.Unknown);
        secondBuild[0].Slot.ShouldBe(GearSlot.Amulet);
    }

    [Fact]
    public void Session_ManyItems_IndependentDraftsDoNotCrossContaminate()
    {
        var items = Enumerable.Range(0, 10)
            .Select(i => Result(new GearItem { ItemPower = i * 10 }))
            .ToList();
        var session = new GearReviewSession(items);

        session.SetSlot(3, GearSlot.Ring);
        session.SetSlot(7, GearSlot.Weapon);

        var built = session.Build();

        built[3].Slot.ShouldBe(GearSlot.Ring);
        built[7].Slot.ShouldBe(GearSlot.Weapon);
        built.Where((_, idx) => idx != 3 && idx != 7)
            .ShouldAllBe(gi => gi.Slot == GearSlot.Unknown);
    }
}

namespace D4LootBench.Core.Tests.Progression;

using D4LootBench.Core.Models;
using Shouldly;

/// <summary>Boundary coverage for <see cref="SlotRuleBuilder"/>, the shared per-slot rule assembler
/// used by both build-guide import and progression generation. Exercises the null-result path,
/// the 15-affix selection cap, required-count clamping, and greater-affix flagging — none of which
/// are reachable end-to-end via <see cref="ProgressionFilterGenerator"/> alone (it pre-caps at 4
/// affixes and never flags per-affix "greater").</summary>
public sealed class SlotRuleBuilderTests
{
    private const uint ItemType = 0x100u;

    private static List<uint> Affixes(int count) =>
        Enumerable.Range(1, count).Select(i => (uint)i).ToList();

    [Fact]
    public void Build_NoItemTypeNoAffixes_ReturnsNull()
    {
        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, null, [], [], 1);

        rule.ShouldBeNull();
    }

    [Fact]
    public void Build_ItemTypeOnlyNoAffixes_ReturnsItemTypeConditionOnly()
    {
        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, ItemType, [], [], 1);

        rule.ShouldNotBeNull();
        rule.Conditions.ShouldHaveSingleItem();
        rule.Conditions[0].ShouldBeOfType<ItemTypeCondition>();
    }

    [Fact]
    public void Build_AffixesOnlyNoItemType_ReturnsAffixConditionOnly()
    {
        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, null, Affixes(3), [], 2);

        rule.ShouldNotBeNull();
        rule.Conditions.ShouldHaveSingleItem();
        rule.Conditions[0].ShouldBeOfType<AffixCondition>();
    }

    [Fact]
    public void Build_BothItemTypeAndAffixes_ReturnsBothConditionsInOrder()
    {
        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, ItemType, Affixes(2), [], 1);

        rule.ShouldNotBeNull();
        rule.Conditions.Count.ShouldBe(2);
        rule.Conditions[0].ShouldBeOfType<ItemTypeCondition>();
        rule.Conditions[1].ShouldBeOfType<AffixCondition>();
    }

    [Theory]
    [InlineData(-5, 1)]  // below min → clamp up to 1
    [InlineData(0, 1)]   // at/below min → clamp up to 1
    [InlineData(1, 1)]   // at min
    [InlineData(2, 2)]   // nominal
    [InlineData(3, 3)]   // at max (== affix count)
    [InlineData(4, 3)]   // just above max → clamp down to affix count
    [InlineData(100, 3)] // way above max → clamp down to affix count
    public void Build_RequiredCountClamped_ToAffixCountRange(int requested, int expected)
    {
        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, null, Affixes(3), [], requested);

        rule!.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(expected);
    }

    [Fact]
    public void Build_SingleAffix_RequiredCountClampsToOneEvenWhenRequestedHigher()
    {
        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, null, Affixes(1), [], 3);

        rule!.Conditions.OfType<AffixCondition>().Single().MinimumCount.ShouldBe(1);
    }

    [Fact]
    public void Build_AffixesAtMaxSelectionCount_KeepsAll()
    {
        var affixes = Affixes(AffixCondition.MaxSelectionCount);

        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, null, affixes, [], 1);

        rule!.Conditions.OfType<AffixCondition>().Single().AffixIds.Count
            .ShouldBe(AffixCondition.MaxSelectionCount);
    }

    [Fact]
    public void Build_AffixesOverMaxSelectionCount_TruncatesToMax()
    {
        var affixes = Affixes(AffixCondition.MaxSelectionCount + 5);

        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, null, affixes, [], 1);

        var affixCondition = rule!.Conditions.OfType<AffixCondition>().Single();
        affixCondition.AffixIds.Count.ShouldBe(AffixCondition.MaxSelectionCount);
        affixCondition.AffixIds.ShouldBe(Affixes(AffixCondition.MaxSelectionCount));
    }

    [Fact]
    public void Build_GreaterAffixIds_FlagsMatchingEntriesOnly()
    {
        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, null, Affixes(3), [2u, 99u], 1);

        var affixCondition = rule!.Conditions.OfType<AffixCondition>().Single();
        affixCondition.GreaterEntries.ShouldHaveSingleItem();
        affixCondition.GreaterEntries[0].AffixId.ShouldBe(2u);
        affixCondition.GreaterEntries[0].AffixIdEcho.ShouldBe(2u);
    }

    [Fact]
    public void Build_GreaterAffixIdBeyondTruncatedCap_NotSurfaced()
    {
        // Affix #(Max+3) would be flagged "greater" but gets dropped by the 15-cap truncation,
        // so it must not leak through as a GreaterAffixEntry either.
        var affixes = Affixes(AffixCondition.MaxSelectionCount + 5);
        var beyondCap = (uint)(AffixCondition.MaxSelectionCount + 3);

        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, null, affixes, [beyondCap], 1);

        rule!.Conditions.OfType<AffixCondition>().Single().GreaterEntries.ShouldBeEmpty();
    }

    [Fact]
    public void Build_EmptyGreaterAffixIds_NoEntriesFlagged()
    {
        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, null, Affixes(2), [], 1);

        rule!.Conditions.OfType<AffixCondition>().Single().GreaterEntries.ShouldBeEmpty();
    }

    [Fact]
    public void Build_AllAffixesFlaggedGreater_AllEntriesPresent()
    {
        var affixes = Affixes(3);

        var rule = SlotRuleBuilder.Build("Test", Visibility.Show, 0u, null, affixes, affixes, 1);

        rule!.Conditions.OfType<AffixCondition>().Single().GreaterEntries.Count.ShouldBe(3);
    }

    // ---- Name-length boundary matrix around MaxNameLength (24) ----
    // D4 silently blanks over-long rule names (they render as "Rule #N"), which is the exact bug this
    // truncation exists to prevent — so these boundaries matter more than a typical string-length cap.

    [Theory]
    [InlineData(23)] // just below max — untouched
    [InlineData(24)] // at max — untouched (name.Length > MaxNameLength is false)
    public void Build_NameAtOrBelowMaxLength_NotTruncated(int length)
    {
        var name = new string('A', length);

        var rule = SlotRuleBuilder.Build(name, Visibility.Show, 0u, null, Affixes(1), [], 1);

        rule!.Name.ShouldBe(name);
        rule.Name.Length.ShouldBe(length);
    }

    [Fact]
    public void Build_NameOneOverMaxLength_TruncatedToMaxLength()
    {
        var name = new string('A', SlotRuleBuilder.MaxNameLength + 1);

        var rule = SlotRuleBuilder.Build(name, Visibility.Show, 0u, null, Affixes(1), [], 1);

        rule!.Name.Length.ShouldBe(SlotRuleBuilder.MaxNameLength);
        rule.Name.ShouldBe(new string('A', SlotRuleBuilder.MaxNameLength));
    }

    [Fact]
    public void Build_TruncationBoundaryLandsOnSpace_TrimEndShortensBelowMaxLength()
    {
        // 23 'A's + a space at index 23 (the 24th char, i.e. exactly the truncation cut point) + more
        // text. The hard cut keeps the space; TrimEnd then removes it, so the final name is 23 chars —
        // shorter than MaxNameLength, but never blank.
        var name = new string('A', 23) + " " + "REST-OF-NAME-BEYOND-CUT";

        var rule = SlotRuleBuilder.Build(name, Visibility.Show, 0u, null, Affixes(1), [], 1);

        rule!.Name.Length.ShouldBe(23);
        rule.Name.ShouldBe(new string('A', 23));
        rule.Name.ShouldNotEndWith(" ");
    }

    [Fact]
    public void Build_NameWhoseFirstMaxLengthCharsAreAllWhitespace_CollapsesToEmptyName()
    {
        // BUG: when the first MaxNameLength characters of an over-long name are entirely whitespace,
        // the hard cut + TrimEnd collapses the name to "". An empty rule name is exactly the "Rule #N"
        // failure mode this whole truncation feature exists to prevent (see SlotRuleBuilder.cs class
        // doc + QA-BRIEF-progression-filter.md). Not reachable via the two current callers today
        // (ProgressionFilterGenerator passes GearSlot.ToString(); BuildGuideFilterGenerator passes an
        // already-.Trim()'d parser SlotLabel) but SlotRuleBuilder.Build is a shared public API with no
        // guard of its own — a future caller (or a parser that stops trimming) reintroduces the bug
        // this feature was built to fix.
        var name = new string(' ', 30) + "Real Name";

        var rule = SlotRuleBuilder.Build(name, Visibility.Show, 0u, null, Affixes(1), [], 1);

        rule!.Name.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Build_SurrogatePairStraddlingTruncationBoundary_DoesNotSplitThePair()
    {
        // A UTF-16 surrogate pair (e.g. an emoji) that straddles index MaxNameLength would be split by
        // a naive ordinal name[..MaxNameLength] cut, leaving a lone unpaired high surrogate — invalid
        // UTF-16 that gets mangled (replacement-charactered) on UTF-8 encode in FilterCodec/ProtoWriter.
        // 23 'A's (indices 0-22) + a surrogate pair at indices 23-24 puts the cut squarely inside it.
        var name = new string('A', 23) + "\U0001F600" + "tail-beyond-the-cut";

        var rule = SlotRuleBuilder.Build(name, Visibility.Show, 0u, null, Affixes(1), [], 1);

        rule!.Name.Length.ShouldBeLessThanOrEqualTo(SlotRuleBuilder.MaxNameLength);
        var lastChar = rule.Name[^1];
        char.IsSurrogate(lastChar).ShouldBeFalse(
            $"Truncated name ends with an unpaired surrogate (U+{(int)lastChar:X4}) — the emoji at the " +
            "cut boundary was split in half, producing invalid UTF-16 that will be mangled on encode.");
    }
}

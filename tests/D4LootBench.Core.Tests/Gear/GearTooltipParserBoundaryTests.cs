using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using Shouldly;

namespace D4LootBench.Core.Tests.Gear;

/// <summary>
/// Boundary-value and heuristic-edge coverage for <see cref="GearTooltipParser"/>, supplementing
/// <see cref="GearTooltipParserTests"/>. Focuses on affix-cap overflow, normalization edge cases
/// (roll ranges, decimals), item-type substring collisions, and malformed/degenerate input.
/// </summary>
public sealed class GearTooltipParserBoundaryTests
{
    private static GearTooltipParser NewParser() => new(new FilterDataService());

    private static IReadOnlyList<string> LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Gear", "Fixtures", name);
        return File.ReadAllLines(path);
    }

    // --- Whitespace / degenerate input boundaries ---

    [Fact]
    public void Parse_WhitespaceOnlyLines_TreatedAsNoReadableText()
    {
        var result = NewParser().Parse(LoadFixture("whitespace-only.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Unknown);
        result.Confidence.ShouldBe(GearParseConfidence.Low);
        result.Warnings.ShouldContain("No readable text found in image.");
    }

    [Fact]
    public void Parse_NullLineEntries_TreatedAsBlank()
    {
        // Simulates an IGearReader implementation that could hand back a null entry in the list.
        var lines = new List<string?> { null, "  ", null }.Select(l => l!).ToList();

        var result = NewParser().Parse(lines);

        result.Confidence.ShouldBe(GearParseConfidence.Low);
        result.Warnings.ShouldContain("No readable text found in image.");
    }

    [Fact]
    public void Parse_SingleUsableLine_BelowThreeLineFloor_IsLowConfidence()
    {
        var result = NewParser().Parse(["Ancestral Legendary Helm"]);

        result.Warnings.ShouldContain("Low-confidence parse — review carefully.");
        result.Confidence.ShouldBe(GearParseConfidence.Low);
    }

    [Fact]
    public void Parse_ExactlyThreeUsableLines_MeetsLineFloor()
    {
        // Three usable lines is the boundary at which the "<3 usable lines" low-confidence
        // trigger no longer fires by itself — other fields must still resolve for High.
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "+30 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Slot.ShouldBe(GearSlot.Ring);
        result.Item.ItemPower.ShouldBe(800);
        result.Confidence.ShouldBe(GearParseConfidence.High);
    }

    // --- Affix cap boundary (MaxAffixes = 6) ---

    [Fact]
    public void Parse_SevenAffixCandidates_CapsAtSixAndWarns()
    {
        var result = NewParser().Parse(LoadFixture("seven-affixes.txt"));

        result.Item.Affixes.Count.ShouldBe(6);
        result.Warnings.ShouldContain(w => w.Contains("More than 6 affix lines detected", StringComparison.Ordinal));
    }

    // --- Normalization edge cases: roll ranges, decimals, multi-number lines ---

    [Fact]
    public void Parse_RollRangeBracketAndDecimal_StrippedBeforeResolution()
    {
        var result = NewParser().Parse(LoadFixture("range-and-decimal-affix.txt"));

        result.Item.Affixes.Count.ShouldBe(2);
        result.Item.Affixes.ShouldContain(a => a.IsResolved && a.ResolvedName == "+Critical Strike Chance");
        result.Item.Affixes.ShouldContain(a => a.IsResolved && a.ResolvedName == "%Cooldown Reduction");
    }

    [Fact]
    public void Parse_ExactCatalogNameWithNoPrefix_ExactMatchesWithoutFuzzy()
    {
        // "Maximum Life" carries no +/% prefix in the catalog, so after normalization stripping
        // the leading "+112 " the phrase is an exact dictionary hit (not merely fuzzy Contains).
        var lines = new[]
        {
            "Ancestral Legendary Boots",
            "750 Item Power",
            "+112 Maximum Life",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].ResolvedName.ShouldBe("Maximum Life");
    }

    [Fact]
    public void Parse_PlusOnlyAffixLine_NormalizesToEmptyAndStaysUnresolved()
    {
        // A line that is just "+" (OCR noise) starts with '+' so it is an affix candidate, but
        // normalization strips it to an empty phrase — must not crash and must report unresolved.
        var lines = new[]
        {
            "Ancestral Legendary Helm",
            "925 Item Power",
            "+",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].IsResolved.ShouldBeFalse();
        result.Item.Affixes[0].RawText.ShouldBe("+");
    }

    // --- Item-type substring collision ---

    [Fact]
    public void Parse_ItemTypeNameInsideAffixLine_BeforeHeaderLine_UsesFirstMatchingLine()
    {
        // Documents the known heuristic limitation: ParseSlot returns on the FIRST line containing
        // any catalog type name, in reading order — even if that line is an affix, not the header.
        // Here "Ring of Fire Resistance Bonus" (garbage affix text) precedes the real header line
        // and itself contains the substring "Ring", so slot still resolves correctly by luck, but
        // the matched line/type name comes from the affix line, not the header.
        var result = NewParser().Parse(LoadFixture("itemtype-collision.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Ring);
        result.Item.ItemTypeName.ShouldBe("Ring");
    }

    [Fact]
    public void Parse_NoItemTypeNameAnywhere_SlotUnknownWithWarning()
    {
        var lines = new[]
        {
            "Some Cosmetic Header Text",
            "800 Item Power",
            "+30 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Slot.ShouldBe(GearSlot.Unknown);
        result.Item.ItemTypeName.ShouldBeNull();
        result.Warnings.ShouldContain("Item slot could not be determined.");
    }

    // --- Weapon keyword mapping boundaries ---

    [Theory]
    [InlineData("Mace", GearSlot.Weapon)]
    [InlineData("Two-Handed Mace", GearSlot.Weapon)]
    [InlineData("Hand Crossbow", GearSlot.Weapon)]
    [InlineData("Crossbow", GearSlot.Weapon)]
    [InlineData("Shield", GearSlot.Offhand)]
    [InlineData("Focus", GearSlot.Offhand)]
    [InlineData("Totem", GearSlot.Offhand)]
    [InlineData("Helm", GearSlot.Helm)]
    [InlineData("Chest Armor", GearSlot.ChestArmor)]
    public void Parse_EachCatalogItemType_MapsToExpectedSlot(string typeName, GearSlot expected)
    {
        var lines = new[]
        {
            $"Ancestral Legendary {typeName}",
            "800 Item Power",
            "+30 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Slot.ShouldBe(expected);
        result.Item.ItemTypeName.ShouldBe(typeName);
    }

    [Fact]
    public void Parse_HandCrossbowLine_DoesNotMatchPlainCrossbowFirst()
    {
        // "Hand Crossbow" (13 chars) must outrank "Crossbow" (8 chars) under longest-name-wins,
        // even though "Crossbow" is a substring of "Hand Crossbow" and would also Contains-match.
        var lines = new[]
        {
            "Ancestral Legendary Hand Crossbow",
            "800 Item Power",
            "+30 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.ItemTypeName.ShouldBe("Hand Crossbow");
        result.Item.Slot.ShouldBe(GearSlot.Weapon);
    }

    // --- Rarity priority boundary ---

    [Fact]
    public void Parse_MultipleRarityKeywordsPresent_MythicOutranksAll()
    {
        var lines = new[]
        {
            "Ancestral Mythic Unique Legendary Rare Helm",
            "925 Item Power",
            "+30 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Rarity.ShouldBe(ItemRarity.Mythic);
    }

    [Fact]
    public void Parse_NoRarityKeyword_ReturnsUnknown()
    {
        var lines = new[]
        {
            "Helm",
            "800 Item Power",
            "+30 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Rarity.ShouldBe(ItemRarity.Unknown);
    }

    // --- Item power boundaries ---

    [Theory]
    [InlineData("9 Item Power", null)] // single digit — below the {2,4} regex floor
    [InlineData("99999 Item Power", 9999)] // 5 digits — regex captures only the first 4
    [InlineData("0 Item Power", null)] // single digit zero — below the floor
    public void Parse_ItemPowerDigitCountBoundaries(string line, int? expected)
    {
        var lines = new[] { "Ancestral Legendary Helm", line, "+30 Dexterity" };

        var result = NewParser().Parse(lines);

        result.Item.ItemPower.ShouldBe(expected);
    }

    [Fact]
    public void Parse_ItemPowerLineWithoutDigits_TreatedAsMissing()
    {
        var lines = new[] { "Ancestral Legendary Helm", "Item Power: unknown", "+30 Dexterity" };

        var result = NewParser().Parse(lines);

        result.Item.ItemPower.ShouldBeNull();
        result.Warnings.ShouldContain("Item power not found.");
    }

    // --- Confidence matrix: each individual Low trigger in isolation ---

    [Fact]
    public void Parse_AllFieldsResolve_ButOnlyUnresolvedAffixes_IsLowConfidence()
    {
        var lines = new[]
        {
            "Ancestral Legendary Helm",
            "925 Item Power",
            "+30 Zzznonexistentaffix",
        };

        var result = NewParser().Parse(lines);

        result.Item.Slot.ShouldBe(GearSlot.Helm);
        result.Item.ItemPower.ShouldBe(925);
        result.Item.Affixes.ShouldAllBe(a => !a.IsResolved);
        result.Confidence.ShouldBe(GearParseConfidence.Low);
    }

    [Fact]
    public void Parse_GoodAffixesAndPowerButUnknownSlot_IsLowConfidence()
    {
        var lines = new[]
        {
            "Some Unrecognized Header",
            "925 Item Power",
            "+112 Maximum Life",
        };

        var result = NewParser().Parse(lines);

        result.Item.Slot.ShouldBe(GearSlot.Unknown);
        result.Item.ItemPower.ShouldBe(925);
        result.Confidence.ShouldBe(GearParseConfidence.Low);
    }

    // --- Culture / casing ---

    [Fact]
    public void Parse_LowercaseItemPowerKeyword_StillMatchesCaseInsensitive()
    {
        var lines = new[] { "Ancestral Legendary Helm", "925 item power", "+30 Dexterity" };

        var result = NewParser().Parse(lines);

        result.Item.ItemPower.ShouldBe(925);
    }

    [Fact]
    public void Parse_UppercaseAncestralMarker_StillDetected()
    {
        var lines = new[] { "ANCESTRAL Legendary Helm", "925 Item Power", "+30 Dexterity" };

        var result = NewParser().Parse(lines);

        result.Item.IsAncestral.ShouldBeTrue();
    }
}

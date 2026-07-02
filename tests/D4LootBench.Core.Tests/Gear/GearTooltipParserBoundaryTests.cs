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

    // --- ExtractBasicAffixBlock structural boundaries ---

    [Fact]
    public void Parse_RareItemWithNoLegendaryPower_OverIncludesTemperedAndGemLines()
    {
        // Documents a known, accepted limitation: a RARE item has no legendary/unique power sentence
        // to terminate the basic-affix run, so tempered affixes and gem/socket "+"-led lines that
        // follow are contiguous with the real basic affixes and get swept in together (up to the
        // MaxAffixes cap). OCR cannot read the bullet icon that would distinguish them in-game.
        var result = NewParser().Parse(LoadFixture("rare-no-legendary-power.txt"));

        result.Item.Rarity.ShouldBe(ItemRarity.Rare);
        result.Item.Affixes.Select(a => a.RawText).ShouldBe(
        [
            "+50 Strength [40 - 50]",
            "+80 Dexterity [70 - 80]",
            "+40 Strength",
            "+40 Strength",
        ]);
    }

    [Fact]
    public void Parse_PlusLedGarbageBeforeItemPowerLine_ExcludedFromAffixBlock()
    {
        // "+20% Ring of Fire Resistance Bonus" sits BEFORE the "Item Power" line in this fixture.
        // The anchor starts the search after Item Power, so this garbage line must never be
        // considered part of the affix block, even though it is itself "+"-led.
        var result = NewParser().Parse(LoadFixture("itemtype-collision.txt"));

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldBe("+30 Dexterity");
    }

    [Fact]
    public void Parse_NoItemPowerLineAtAll_FallsBackToTopOfTooltipScan()
    {
        // Cropped tooltip with no "Item Power" line anywhere: the anchor search must start at index 0
        // instead of never finding a start, so both leading "+"-led lines are still captured.
        var result = NewParser().Parse(LoadFixture("cropped-partial.txt"));

        result.Item.ItemPower.ShouldBeNull();
        result.Item.Affixes.Select(a => a.RawText).ShouldBe(
        [
            "+120 Maximum Life",
            "+40% Vulnerable Damage",
        ]);
    }

    [Fact]
    public void Parse_IconMisreadAsLetterBeforePlus_LineIsSkippedEntirely_NotJustExcluded()
    {
        // If OCR renders a bullet icon as a stray LETTER immediately before the '+', IsAffixLine sees
        // an alphanumeric character before '+' and returns false for the WHOLE line — the affix isn't
        // just mis-anchored, it disappears entirely; the run instead starts at the next real "+" line.
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "e+99 Strength",
            "+30 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldBe("+30 Dexterity");
    }

    [Fact]
    public void Parse_UniquePowerLineStartingWithPlus_MergesIntoAffixRun()
    {
        // Documents a known, accepted limitation: some unique-power sentences themselves start with
        // "+" (e.g. "+2 to All Skills"). Because the run only stops at the first NON-"+"-led line,
        // such a power line is indistinguishable from a real affix and merges into the run along with
        // whatever follows it.
        var lines = new[]
        {
            "Ancestral Unique Ring",
            "800 Item Power",
            "+30 Dexterity",
            "+2 to All Skills",
            "+40 Maximum Life",
            "Requires Level 60",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Select(a => a.RawText).ShouldBe(
        [
            "+30 Dexterity",
            "+2 to All Skills",
            "+40 Maximum Life",
        ]);
    }

    [Fact]
    public void Parse_ExactlySixContiguousAffixLines_AllKeptNoOverflowWarning()
    {
        // Complement to the seven-line overflow test: exactly MaxAffixes (6) contiguous "+"-led
        // lines must all be kept with no truncation warning.
        var lines = new[]
        {
            "Ancestral Legendary Amulet",
            "900 Item Power",
            "+45.0% Critical Strike Chance",
            "+112 Maximum Life",
            "+8.5% Cooldown Reduction",
            "+30 Dexterity",
            "+15% Critical Strike Damage",
            "+50% Damage",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(6);
        result.Warnings.ShouldNotContain(w => w.Contains("More than", StringComparison.Ordinal));
    }

    // --- Match-rate capture-quality signal (MatchRateThreshold = 0.5, floor = 3 candidates) ---

    [Fact]
    public void Parse_MajorityAffixesUnresolved_WarnsPoorCaptureAndIsLow()
    {
        // 1 of 3 affixes resolve → match rate 0.33 < 0.5 with ≥3 candidates: aggregate poor-capture signal.
        var lines = new[]
        {
            "Ancestral Legendary Helm",
            "925 Item Power",
            "+112 Maximum Life",
            "+30 Zzznonexistentaffix",
            "+40 Qqqbogusaffix",
        };

        var result = NewParser().Parse(lines);

        result.Confidence.ShouldBe(GearParseConfidence.Low);
        result.Warnings.ShouldContain(w => w.Contains("of 3 affixes were recognized", StringComparison.Ordinal));
        result.Warnings.ShouldContain(w => w.Contains("cropped", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_MajorityAffixesResolved_NoPoorCaptureWarning()
    {
        // 2 of 3 affixes resolve → match rate 0.67 ≥ 0.5: no aggregate warning, stays High.
        var lines = new[]
        {
            "Ancestral Legendary Helm",
            "925 Item Power",
            "+112 Maximum Life",
            "+30 Dexterity",
            "+40 Qqqbogusaffix",
        };

        var result = NewParser().Parse(lines);

        result.Warnings.ShouldNotContain(w => w.Contains("affixes were recognized", StringComparison.Ordinal));
        result.Confidence.ShouldBe(GearParseConfidence.High);
    }

    [Fact]
    public void Parse_TwoAffixesBelowSampleFloor_NoPoorCaptureWarning()
    {
        // Only 2 affix candidates (below MatchRateFloorSampleSize = 3): the rate is not meaningful, so
        // no aggregate warning — but zero resolved still forces Low via the resolvedCount == 0 trigger.
        var lines = new[]
        {
            "Ancestral Legendary Helm",
            "925 Item Power",
            "+30 Qqq",
            "+40 Zzz",
        };

        var result = NewParser().Parse(lines);

        result.Warnings.ShouldNotContain(w => w.Contains("affixes were recognized", StringComparison.Ordinal));
        result.Confidence.ShouldBe(GearParseConfidence.Low);
    }

    [Fact]
    public void Parse_AllAffixesUnresolvedAtSampleFloor_WarnsPoorCapture()
    {
        // 0 of 3 resolve → rate 0.0 with exactly the sample floor: poor-capture warning present and Low.
        var lines = new[]
        {
            "Ancestral Legendary Helm",
            "925 Item Power",
            "+30 Qqq",
            "+40 Zzz",
            "+50 Www",
        };

        var result = NewParser().Parse(lines);

        result.Warnings.ShouldContain(w => w.Contains("of 3 affixes were recognized", StringComparison.Ordinal));
        result.Confidence.ShouldBe(GearParseConfidence.Low);
    }

    [Fact]
    public void Parse_ExactlyHalfAffixesResolved_AtThresholdBoundary_DoesNotWarn()
    {
        // 2 of 4 resolve → rate is EXACTLY 0.5. MatchRateThreshold comparison is strict '<', so 0.5
        // must NOT trip the poor-capture warning (only rates strictly below 0.5 do).
        var lines = new[]
        {
            "Ancestral Legendary Helm",
            "925 Item Power",
            "+112 Maximum Life",
            "+30 Dexterity",
            "+40 Qqqbogusaffix",
            "+50 Wwwbogusaffix",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(4);
        result.Warnings.ShouldNotContain(w => w.Contains("affixes were recognized", StringComparison.Ordinal));
        result.Confidence.ShouldBe(GearParseConfidence.High);
    }

    [Fact]
    public void Parse_MatchRateDenominatorUsesPostCapCount_NotRawCandidateCount()
    {
        // 7 contiguous affix candidates overflow MaxAffixes (6): the 7th is dropped BEFORE the match-rate
        // calculation runs, so the denominator is 6 (post-cap), not 7 (raw). Only 1 of the kept 6 resolves,
        // so the rate is 1/6 (~0.17), not 1/7 — both the overflow warning and the poor-capture warning
        // must fire together, and the message must read "1 of 6", never "1 of 7".
        var lines = new[]
        {
            "Ancestral Legendary Helm",
            "925 Item Power",
            "+112 Maximum Life",
            "+30 Qqqbogusaffix",
            "+40 Wwwbogusaffix",
            "+50 Zzznonexistentaffix",
            "+60 Eeebogusaffix",
            "+70 Rrrbogusaffix",
            "+80 Tttbogusaffix",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(6);
        result.Warnings.ShouldContain(w => w.Contains("More than 6 affix lines detected", StringComparison.Ordinal));
        result.Warnings.ShouldContain(w => w.Contains("Only 1 of 6 affixes were recognized", StringComparison.Ordinal));
        result.Warnings.ShouldNotContain(w => w.Contains("of 7 affixes", StringComparison.Ordinal));
        result.Confidence.ShouldBe(GearParseConfidence.Low);
    }
}

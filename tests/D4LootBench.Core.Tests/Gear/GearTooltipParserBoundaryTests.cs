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
    public void Parse_IconMisreadAsLetterBeforePlus_LineStillCapturedViaMarker()
    {
        // If OCR renders a bullet icon as a stray LETTER immediately before the '+', membership now keys
        // off the affix marker ('+') anywhere on the line rather than a leading '+', so the affix is
        // captured instead of dropped — the whole point of the marker-based model: don't lose real affixes
        // to leading OCR junk.
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "e+99 Strength",
            "+30 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Select(a => a.RawText).ShouldBe(
        [
            "e+99 Strength",
            "+30 Dexterity",
        ]);
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

    // --- Continuation-tail merge boundaries (HasUnclosedBracket / IsContinuationTail chain) ---

    [Fact]
    public void Parse_ChainedUnclosedBracketLines_MergesAllThreeIntoSingleAffix()
    {
        // Three OCR lines in a row: an affix that opens '[' but never closes it, then two bare numeric
        // tails. Every intermediate merge still leaves the bracket unclosed ("[10 - 20"), so the SECOND
        // tail must also be folded in rather than starting its own (nonsensical) line.
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "+50 Something [10 -",
            "20",
            "30]",
            "+40 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(2);
        result.Item.Affixes[0].RawText.ShouldBe("+50 Something [10 - 20 30]");
        result.Item.Affixes[1].RawText.ShouldBe("+40 Dexterity");
        result.Item.Affixes[1].IsResolved.ShouldBeTrue();
    }

    [Fact]
    public void Parse_UnclosedBracketNeverCloses_StillAdmitsNextRealAffixSeparately()
    {
        // The bracket opened on the first affix is never closed by anything downstream. The next line
        // starts with '+' (a real new affix), so IsContinuationTail must reject it and it must be kept
        // as its own, separately-resolved affix rather than being swallowed into the unclosed line.
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "+50 Something [10 -",
            "+40 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(2);
        result.Item.Affixes[0].RawText.ShouldBe("+50 Something [10 -");
        result.Item.Affixes[0].IsResolved.ShouldBeFalse();
        result.Item.Affixes[1].RawText.ShouldBe("+40 Dexterity");
        result.Item.Affixes[1].IsResolved.ShouldBeTrue();
    }

    [Fact]
    public void Parse_DecimalContinuationTail_RejoinsAndResolves()
    {
        // Complement to the integer-tail fixture (riveted-helm-wrapped): the wrapped tail itself carries
        // a decimal ("8.0]%"), exercising the '.' character in ContinuationTailRegex specifically.
        var lines = new[]
        {
            "Ancestral Legendary Amulet",
            "900 Item Power",
            "+8.0% Cooldown Reduction [5.0 -",
            "8.0]%",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldBe("+8.0% Cooldown Reduction [5.0 - 8.0]%");
        result.Item.Affixes[0].IsResolved.ShouldBeTrue();
    }

    [Fact]
    public void Parse_BareRangeTailWithNoPrecedingBlockEntry_IsSilentlyDropped()
    {
        // Known limitation: the continuation merge only fires when there is already a PRIOR block entry
        // to fold onto (block.Count > 0). If a wrapped range tail is the very first line encountered
        // after the Item Power anchor — nothing came before it in the block yet — it has no '+'/'%'/'['
        // marker of its own, fails IsAffixLine, and is silently skipped rather than merged or reported.
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "259]",
            "+40 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldBe("+40 Dexterity");
    }

    [Fact]
    public void Parse_NumericSentenceTailAfterUnclosedBracket_SilentlyMergesIntoAffixRawText()
    {
        // Known limitation (silent over-capture): the continuation-tail merge runs BEFORE IsAffixLine's
        // trailing-'.' sentence gate ever sees the line. A legendary/unique power sentence whose OCR-wrapped
        // tail happens to be pure digits/%/period (e.g. "...by 20%.") is indistinguishable from a genuine
        // range-tail once the previous line has an unclosed '[', so it gets folded into the affix's
        // RawText instead of terminating the block. This test pins the current (accepted) behavior so a
        // future change to the merge/gate ordering is a deliberate decision, not a silent regression.
        var lines = new[]
        {
            "Ancestral Unique Ring",
            "800 Item Power",
            "+30 Dexterity [20 -",
            "20%.",
            "+40 Maximum Life",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(2);
        result.Item.Affixes[0].RawText.ShouldBe("+30 Dexterity [20 - 20%.");
        result.Item.Affixes[1].RawText.ShouldBe("+40 Maximum Life");
    }

    // --- Degenerate single-marker-character lines ---

    [Fact]
    public void Parse_BareOpenBracketLine_CapturedAsUnresolvedAffixWithoutCrashing()
    {
        // A line that is JUST "[" carries the '[' marker so IsAffixLine admits it, but normalization
        // (RangeRegex matching the bracket with nothing after it) strips it to an empty phrase — must not
        // throw and must surface as unresolved, mirroring the existing "+"-alone coverage.
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "[",
            "+40 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(2);
        result.Item.Affixes[0].RawText.ShouldBe("[");
        result.Item.Affixes[0].IsResolved.ShouldBeFalse();
    }

    [Fact]
    public void Parse_BarePercentLine_CapturedAsUnresolvedAffixWithoutCrashing()
    {
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "%",
            "+40 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(2);
        result.Item.Affixes[0].RawText.ShouldBe("%");
        result.Item.Affixes[0].IsResolved.ShouldBeFalse();
    }

    // --- Marker-based membership admits non-affix base stats (accepted limitation) ---

    [Fact]
    public void Parse_ShieldBlockChanceBaseStat_AdmittedIntoAffixBlockAsUnresolved()
    {
        // Documents an explicitly accepted limitation: "30% Block Chance" is a base stat, not a rollable
        // affix, but it carries the '%' marker and sits contiguous with real affixes, so it is now admitted
        // (previously excluded under the old '+'-led-only scan). It stays unresolved since it is not in the
        // affix catalog, which is the guard rail against it silently corrupting resolved data.
        var lines = new[]
        {
            "Ancestral Legendary Shield",
            "800 Item Power",
            "+30 Dexterity",
            "30% Block Chance",
            "+40 Maximum Life",
        };

        var result = NewParser().Parse(lines);

        result.Item.Slot.ShouldBe(GearSlot.Offhand);
        result.Item.Affixes.Count.ShouldBe(3);
        result.Item.Affixes[1].RawText.ShouldBe("30% Block Chance");
        result.Item.Affixes[1].IsResolved.ShouldBeFalse();
    }

    // --- Weapon base-damage block exclusion ---

    [Fact]
    public void Parse_WeaponBaseDamageLines_ExcludedSoRealAffixesAreReached()
    {
        // A weapon tooltip prints its base-damage properties ("Damage Per Second", the bracketed
        // "Damage per Hit" range, and "Attacks per Second") directly under Item Power, BEFORE the real
        // affixes. The "Damage per Hit" line carries a '[' roll-range marker and the "Attacks per Second"
        // line carries none — so the marker scan used to admit the former as the first affix and let the
        // latter terminate the block, cutting off the actual affixes below (the "[14 - 20]%" multiplier
        // among them). Base-damage lines must be excluded so the real affixes are still captured.
        var result = NewParser().Parse(LoadFixture("weapon-base-damage.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Weapon);
        result.Item.Affixes.Select(a => a.RawText).ShouldBe(
        [
            "+287 Weapon Damage [187 - 312]",
            "x14% Physical Damage Multiplier [14 - 20]%",
        ]);
    }

    // --- OCR glyph correction + fully-wrapped range continuation ---

    [Fact]
    public void Parse_FullyWrappedRangeLine_MergesIntoPreviousAffix()
    {
        // The entire roll range wrapped onto its own line ("[10 - 20]%"). A '['-led line is never a
        // genuine affix, so it must fold onto the previous affix even though that line's bracket is closed.
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "+50 Something",
            "[10 - 20]%",
            "+40 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(2);
        result.Item.Affixes[0].RawText.ShouldBe("+50 Something [10 - 20]%");
        result.Item.Affixes[1].RawText.ShouldBe("+40 Dexterity");
        result.Item.Affixes[1].IsResolved.ShouldBeTrue();
    }

    [Fact]
    public void Parse_PercentAfterBracketMisreadAsSlashPair_CorrectedToPercent()
    {
        // OCR misread the closing '%' of a range as a slash pair ("]0/0"). The glyph pass must rewrite
        // it back to "]%" so the RawText is clean and the affix still resolves.
        var lines = new[]
        {
            "Ancestral Legendary Amulet",
            "900 Item Power",
            "+8.0% Cooldown Reduction [5.0 - 8.0]0/0",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldEndWith("[5.0 - 8.0]%");
        result.Item.Affixes[0].IsResolved.ShouldBeTrue();
    }

    [Fact]
    public void Parse_VerticalBarsInNumber_CorrectedToDigitOne()
    {
        // OCR read the digit '1' as a vertical bar ('|') twice. Every '|' maps back to '1'.
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "+| |% Critical Strike Chance",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldBe("+1 1% Critical Strike Chance");
        result.Item.Affixes[0].IsResolved.ShouldBeTrue();
    }

    [Fact]
    public void Parse_LeadingMultiplierX_StrippedSoMultiplierResolves()
    {
        // The leading "x" of a damage multiplier ("x36%") survives the numeric strip as a lone token
        // that derails resolution — it must be dropped so the bare stat name resolves.
        var lines = new[]
        {
            "Ancestral Legendary Amulet",
            "900 Item Power",
            "x36% Critical Strike Damage Multiplier [26 - 50]%",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].IsResolved.ShouldBeTrue();
        result.Item.Affixes[0].ResolvedName!.ShouldContain("Critical Strike Damage Multiplier");
    }

    [Fact]
    public void Parse_BracketLedLineAsFirstBlockEntry_StaysStandaloneNotMerged()
    {
        // Regression guard: a '['-led line that is the FIRST block entry (block.Count == 0) has nothing
        // to merge onto, so it falls through to IsAffixLine and is kept as its own unresolved affix —
        // the continuation merge must not fire without a prior entry.
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "[26 - 50]%",
            "+40 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(2);
        result.Item.Affixes[0].RawText.ShouldBe("[26 - 50]%");
        result.Item.Affixes[0].IsResolved.ShouldBeFalse();
    }

    [Fact]
    public void Parse_VerticalBarInItemPowerLine_StillReadsPower()
    {
        // Global correction runs on every line, so a '|' misread in the item-power line ("9|1") is
        // repaired before power extraction — proving the glyph pass is not affix-only.
        var lines = new[]
        {
            "Ancestral Legendary Helm",
            "9|1 Item Power",
            "+30 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.ItemPower.ShouldBe(911);
    }

    // --- Unicode / non-ASCII content ---

    [Fact]
    public void Parse_UnicodeCharactersInAffixLine_DoesNotCrashAndStaysUnresolved()
    {
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "+40 Dexterité ★",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].IsResolved.ShouldBeFalse();
        result.Confidence.ShouldBe(GearParseConfidence.Low);
    }

    // --- Weapon-keyword fallback (ParseSlot), Glaive complement to the Flail test ---

    [Fact]
    public void Parse_GlaiveWeapon_MapsToWeaponSlotWithGlaiveTypeName()
    {
        // Glaive (Spiritborn), like Flail, is not yet in the filterable item-type catalog — complements
        // Parse_FlailWeapon_MapsToWeaponSlot in GearTooltipParserTests to cover both newly-added keywords.
        var lines = new[]
        {
            "Ancestral Legendary Glaive",
            "800 Item Power",
            "+40 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Slot.ShouldBe(GearSlot.Weapon);
        result.Item.ItemTypeName.ShouldBe("Glaive");
    }

    // --- PercentAfterBracketRegex variants (]0/0, ]0/6, ]o/o, ]O/0 …) ---

    [Theory]
    [InlineData("+8.0% Cooldown Reduction [5.0 - 8.0]0/6")] // digit six second-slot variant, called out in the design doc
    [InlineData("+8.0% Cooldown Reduction [5.0 - 8.0]o/o")] // lowercase 'o' both slots
    [InlineData("+8.0% Cooldown Reduction [5.0 - 8.0]O/0")] // uppercase 'O' first slot
    public void Parse_PercentAfterBracketSlashVariant_CorrectedToPercent(string affixLine)
    {
        var lines = new[] { "Ancestral Legendary Amulet", "900 Item Power", affixLine };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldBe("+8.0% Cooldown Reduction [5.0 - 8.0]%");
        result.Item.Affixes[0].IsResolved.ShouldBeTrue();
    }

    [Fact]
    public void Parse_MultiDigitFirstTokenAfterBracket_NotCorrected()
    {
        // Guard: PercentAfterBracketRegex only matches a SINGLE [0oO] char in the first slash slot.
        // "]12/34" must survive untouched — a two-digit first token is never a '%' misread.
        var lines = new[]
        {
            "Ancestral Legendary Amulet",
            "900 Item Power",
            "+8.0% Cooldown Reduction [5.0 - 8.0]12/34",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldBe("+8.0% Cooldown Reduction [5.0 - 8.0]12/34");
        result.Item.Affixes[0].IsResolved.ShouldBeFalse();
    }

    [Fact]
    public void Parse_SlashPairWithoutPrecedingBracket_NotCorrected()
    {
        // Guard: the regex is anchored to a slash pair IMMEDIATELY after ']'. A slash pair elsewhere
        // in the line (no preceding bracket) must be left alone.
        var lines = new[]
        {
            "Ancestral Legendary Amulet",
            "900 Item Power",
            "+30% Something 0/0 Bonus",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldBe("+30% Something 0/0 Bonus");
    }

    // --- Blanket '|' -> '1' correction: false-positive risk on legitimate alphabetic OCR words ---

    [Fact]
    public void Parse_VerticalBarMisreadInsideItemTypeWord_BreaksSlotDetection()
    {
        // KNOWN GAP (not merely a documented limitation — a plausible real OCR failure mode): the
        // blanket '|' -> '1' correction assumes every '|' originates from a misread DIGIT '1'. In many
        // fonts a lowercase 'l' (as in "Helm") renders as a vertical stroke OCR can also misread as '|'.
        // Once corrected, "He|m" becomes "He1m", which no longer Contains-matches the "Helm" catalog
        // entry, so slot detection silently fails on a tooltip that would otherwise have been readable.
        var lines = new[]
        {
            "Ancestral Legendary He|m",
            "800 Item Power",
            "+30 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Slot.ShouldBe(GearSlot.Unknown);
        result.Item.ItemTypeName.ShouldBeNull();
        result.Warnings.ShouldContain("Item slot could not be determined.");
    }

    [Fact]
    public void Parse_VerticalBarMisreadInsideAffixWord_BreaksResolution()
    {
        // Same root cause as the item-type case, on the affix path: "Willpower" (a real primary stat)
        // misread with a '|' for the 'l' becomes "Wi|lpower" -> corrected to "Wi1lpower", which the
        // resolver cannot match, so a real recognized affix silently degrades to unresolved.
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "+30 Wi|lpower",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldBe("+30 Wi1lpower");
        result.Item.Affixes[0].IsResolved.ShouldBeFalse();
    }

    // --- Leading multiplier strip: case and unicode variants of the 'x' sign ---

    [Fact]
    public void Parse_UppercaseXMultiplier_Stripped()
    {
        var lines = new[]
        {
            "Ancestral Legendary Amulet",
            "900 Item Power",
            "X36% Critical Strike Damage Multiplier [26 - 50]%",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].IsResolved.ShouldBeTrue();
        result.Item.Affixes[0].ResolvedName!.ShouldContain("Critical Strike Damage Multiplier");
    }

    [Fact]
    public void Parse_UnicodeMultiplicationSignMultiplier_Stripped()
    {
        // D4's actual multiplier glyph is closer to '×' (U+00D7) than ascii 'x' on some captures.
        var lines = new[]
        {
            "Ancestral Legendary Amulet",
            "900 Item Power",
            "×36% Critical Strike Damage Multiplier [26 - 50]%",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].IsResolved.ShouldBeTrue();
        result.Item.Affixes[0].ResolvedName!.ShouldContain("Critical Strike Damage Multiplier");
    }

    // --- MaxAffixes cap interplay with range-continuation merging ---

    [Fact]
    public void Parse_MergedRangesReduceBlockBelowCap_NoOverflowWarningDespiteManyRawLines()
    {
        // 10 raw OCR lines, but 3 are wrapped-range tails that merge back onto their affix, leaving
        // exactly 6 block entries (at, not over, MaxAffixes). The overflow warning must NOT fire — the
        // cap is checked post-merge against block.Count, never against the raw physical line count.
        var lines = new[]
        {
            "Ancestral Legendary Amulet",
            "900 Item Power",
            "+45.0% Critical Strike Chance [40.0 -",
            "45.0]%",
            "+112 Maximum Life [100 -",
            "112]",
            "+8.5% Cooldown Reduction [8.0 -",
            "8.5]%",
            "+30 Dexterity",
            "+15% Critical Strike Damage",
            "+50% Damage",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(6);
        result.Item.Affixes[0].RawText.ShouldBe("+45.0% Critical Strike Chance [40.0 - 45.0]%");
        result.Warnings.ShouldNotContain(w => w.Contains("More than", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_MergedRangesStillOverflowCap_WarnsWithPostMergeCount()
    {
        // One merge (2 raw lines -> 1 block entry) still leaves 7 post-merge block entries — over the
        // cap — so the overflow warning must fire and the kept affixes must be truncated to 6.
        var lines = new[]
        {
            "Ancestral Legendary Amulet",
            "900 Item Power",
            "+45.0% Critical Strike Chance [40.0 -",
            "45.0]%",
            "+112 Maximum Life",
            "+8.5% Cooldown Reduction",
            "+30 Dexterity",
            "+15% Critical Strike Damage",
            "+50% Damage",
            "+60% Attack Speed",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.Count.ShouldBe(6);
        result.Warnings.ShouldContain(w => w.Contains("More than 6 affix lines detected", StringComparison.Ordinal));
    }

    // --- Stray orphan bracket line: not a marker line, not a continuation tail ---

    [Fact]
    public void Parse_StrayClosingBracketAfterClosedRange_TerminatesBlockEarlyDroppingTrailingAffix()
    {
        // KNOWN GAP: a bare "]" line has no unclosed '[' to close (the previous affix's range is already
        // balanced) so IsRangeContinuation's unclosed-bracket path is false, and it doesn't start with
        // '[' itself so the fully-wrapped path is also false — it is not a continuation. It also carries
        // none of '+'/'%'/'[' so IsAffixLine rejects it too. Once the block has started, this non-affix,
        // non-continuation line ends the block via the `else if (started) break;` path, silently dropping
        // every real affix below it (here, "+40 Dexterity" never appears in the result).
        var lines = new[]
        {
            "Ancestral Legendary Ring",
            "800 Item Power",
            "+50 Something [10 - 20]",
            "]",
            "+40 Dexterity",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldBe("+50 Something [10 - 20]");
        result.Item.Affixes.ShouldNotContain(a => a.RawText == "+40 Dexterity");
    }

    // --- Glyph correction must run before the continuation-tail regex sees the line ---

    [Fact]
    public void Parse_ContinuationTailNeedingGlyphCorrection_MergesAfterCorrection()
    {
        // The wrapped tail itself carries the slash-pair glyph defect ("8.0]0/6"). ContinuationTailRegex
        // has no '/' or 'o'/'O' in its character class, so the RAW tail would fail IsContinuationTail —
        // the merge only succeeds because CorrectOcrGlyphs runs on every line before ExtractBasicAffixBlock
        // ever sees it, turning "8.0]0/6" into "8.0]%" first.
        var lines = new[]
        {
            "Ancestral Legendary Amulet",
            "900 Item Power",
            "+8.0% Cooldown Reduction [5.0 -",
            "8.0]0/6",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldHaveSingleItem();
        result.Item.Affixes[0].RawText.ShouldBe("+8.0% Cooldown Reduction [5.0 - 8.0]%");
        result.Item.Affixes[0].IsResolved.ShouldBeTrue();
    }
}

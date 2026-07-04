using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using Shouldly;

namespace D4LootBench.Core.Tests.Gear;

public sealed class GearTooltipParserTests
{
    private static GearTooltipParser NewParser() => new(new FilterDataService());

    private static IReadOnlyList<string> LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Gear", "Fixtures", name);
        return File.ReadAllLines(path);
    }

    [Fact]
    public void Parse_LegendaryHelm_ExtractsAllFields()
    {
        var result = NewParser().Parse(LoadFixture("legendary-helm.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Helm);
        result.Item.Rarity.ShouldBe(ItemRarity.Legendary);
        result.Item.IsAncestral.ShouldBeTrue();
        result.Item.ItemPower.ShouldBe(925);
        result.Item.Affixes.Count(a => a.IsResolved).ShouldBeGreaterThanOrEqualTo(2);
        result.Confidence.ShouldBe(GearParseConfidence.High);
    }

    [Fact]
    public void Parse_FullTooltip_ExtractsOnlyBasicAffixes()
    {
        // Realistic tooltip with base stats, roll-range brackets, a legendary power sentence, a tempered
        // affix, and gem/socket bonuses. Only the four "+"-led basic affixes above the legendary power
        // must be captured — base stats, the legendary line, the tempered affix, and gems are excluded.
        var result = NewParser().Parse(LoadFixture("legendary-pants-full.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Pants);
        result.Item.Affixes.Select(a => a.RawText).ShouldBe(
        [
            "+99 Strength [83 - 99]",
            "+1,225 Maximum Life [1,016 - 1,225]",
            "+4 Fury Regeneration [3 - 4]",
            "+1,962 Armor [1,561 - 1,962]",
        ]);
    }

    [Fact]
    public void Parse_WrappedRange_RejoinsSplitLineAndCapturesDigitLedAffix()
    {
        // OCR wrapped the first affix's roll range onto its own line ("259]") and the last affix leads with
        // a digit ("8.0% Cooldown Reduction") — the old "+"-led scan dropped both. All three must survive.
        var result = NewParser().Parse(LoadFixture("riveted-helm-wrapped.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Helm);
        result.Item.Affixes.Count.ShouldBe(3);
        result.Item.Affixes[0].RawText.ShouldBe("+246 Resistance to All Elements [229 - 259]");
        result.Item.Affixes.ShouldAllBe(a => a.IsResolved);
    }

    [Fact]
    public void Parse_CutOffRanges_CapturesAllAffixesDespiteTruncation()
    {
        // A cropped screenshot left two ranges with an unclosed "[" ("[15 -"). Membership keys off the
        // affix marker, not a closed range, so all three affixes are captured — including the uncatalogued
        // "Fortify Generation", which is kept unresolved rather than dropped.
        var result = NewParser().Parse(LoadFixture("rare-boots-cutoff.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Boots);
        result.Item.Affixes.Count.ShouldBe(3);
        var raws = result.Item.Affixes.Select(a => a.RawText).ToList();
        raws.ShouldContain(r => r.Contains("Movement Speed"));
        raws.ShouldContain(r => r.Contains("Fortify Generation"));
        raws.ShouldContain(r => r.Contains("War Cry"));
        result.Item.Affixes.ShouldContain(
            a => a.IsResolved && a.ResolvedName!.Contains("Movement Speed"));
    }

    [Fact]
    public void Parse_TwoAffixRing_ResolvesAffixes()
    {
        var result = NewParser().Parse(LoadFixture("ring-two-affix.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Ring);
        result.Item.Affixes.ShouldAllBe(a => a.IsResolved);
        result.Item.Affixes.ShouldAllBe(a => !a.IsGreaterAffix);
        result.Item.Affixes.Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_MissingAffixes_StillReturnsItem()
    {
        var result = NewParser().Parse(LoadFixture("missing-affixes.txt"));

        result.Item.Affixes.ShouldBeEmpty();
        result.Warnings.ShouldContain(w => w.Contains("affix", StringComparison.OrdinalIgnoreCase));
        result.Confidence.ShouldBe(GearParseConfidence.Low);
    }

    [Fact]
    public void Parse_Garbage_LowConfidence()
    {
        var result = NewParser().Parse(LoadFixture("garbage.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Unknown);
        result.Item.ItemPower.ShouldBeNull();
        result.Confidence.ShouldBe(GearParseConfidence.Low);
        result.Warnings.ShouldNotBeEmpty();
    }

    [Fact]
    public void Parse_CroppedPartial_LowConfidence()
    {
        var result = NewParser().Parse(LoadFixture("cropped-partial.txt"));

        result.Confidence.ShouldBe(GearParseConfidence.Low);
        result.Warnings.ShouldContain("Low-confidence parse — review carefully.");
        result.Item.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_UnusualWeapon_MapsToWeaponSlot()
    {
        var result = NewParser().Parse(LoadFixture("unusual-weapon.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Weapon);
        result.Item.ItemTypeName.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_FlailWeapon_MapsToWeaponSlot()
    {
        // Flail is now in the filterable item-type catalog, so the catalog scan matches it directly
        // (still mapped to GearSlot.Weapon via the weapon-keyword arm of MapItemTypeToSlot).
        var result = NewParser().Parse(LoadFixture("flail-weapon.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Weapon);
        result.Item.ItemTypeName.ShouldBe("Flail");
    }

    [Fact]
    public void Parse_PolearmWithWrappedAndGlyphMangledRanges_RejoinsAllAndResolvesMultiplier()
    {
        // Real capture defects on one item: the Critical Strike range fully wrapped onto its own line
        // ("[26 - 50]…"), its closing '%' misread as "0/6", and the Physical Damage range half-wrapped
        // ("[14 -" + "20]%"). All three must fold back onto their affixes, and the leading multiplier
        // "x" must be stripped so the Critical Strike Damage Multiplier resolves.
        var result = NewParser().Parse(LoadFixture("silent-charge-polearm.txt"));

        result.Item.Slot.ShouldBe(GearSlot.Weapon);
        result.Item.Affixes.Select(a => a.RawText).ShouldBe(
        [
            "+284 Weapon Damage [187 - 312]",
            "x36% Critical Strike Damage Multiplier [26 - 50]%",
            "x20% Physical Damage Multiplier [14 - 20]%",
        ]);
        result.Item.Affixes.ShouldContain(
            a => a.IsResolved && a.ResolvedName!.Contains("Critical Strike Damage"));
    }

    [Fact]
    public void Parse_ImperfectAffixText_FuzzyResolves()
    {
        // "Maximum Lif" is a mangled OCR of "Maximum Life" — resolves via the NameResolver fuzzy path.
        var lines = new[]
        {
            "Ancestral Legendary Gloves",
            "850 Item Power",
            "+118 Maximum Lif",
        };

        var result = NewParser().Parse(lines);

        result.Item.Affixes.ShouldContain(a => a.IsResolved);
    }

    [Fact]
    public void Parse_Empty_ReturnsNoTextWarning()
    {
        var result = NewParser().Parse([]);

        result.Item.Slot.ShouldBe(GearSlot.Unknown);
        result.Item.ItemPower.ShouldBeNull();
        result.Confidence.ShouldBe(GearParseConfidence.Low);
        result.Warnings.ShouldContain("No readable text found in image.");
    }

    [Fact]
    public async Task FakeGearReader_ReturnsFixtureLines()
    {
        var reader = new FakeGearReader(LoadFixture("legendary-helm.txt"));
        using var stream = new MemoryStream();

        var lines = await reader.ReadLinesAsync(stream);
        var result = NewParser().Parse(lines);

        result.Item.Slot.ShouldBe(GearSlot.Helm);
        result.Item.ItemPower.ShouldBe(925);
    }
}

using D4LootBench.Core.Import;
using Shouldly;

namespace D4LootBench.Core.Tests.Import;

public sealed class BuildGuideParserTests
{
    // ── Format detection ─────────────────────────────────────────────────────

    [Fact]
    public void Importer_DetectsMobalytics_WhenToggleModifiersPresent()
    {
        var result = new BuildGuideImporter().Import(MobalyticsFixture);
        result.DetectedFormat.ShouldBe(BuildGuideFormat.Mobalytics);
    }

    [Fact]
    public void Importer_DetectsMaxroll_WhenFirstLineIsSlotKeyword()
    {
        var result = new BuildGuideImporter().Import(MaxrollFixture);
        result.DetectedFormat.ShouldBe(BuildGuideFormat.Maxroll);
    }

    [Fact]
    public void Importer_DetectsIcyVeins_WhenGearAffixesHeaderPresent()
    {
        var result = new BuildGuideImporter().Import(IcyVeinsFixture);
        result.DetectedFormat.ShouldBe(BuildGuideFormat.IcyVeins);
    }

    [Fact]
    public void Importer_ThrowsForUnknownFormat()
    {
        Should.Throw<BuildGuideImportException>(() =>
            new BuildGuideImporter().Import("some random text that matches nothing"));
    }

    [Fact]
    public void Importer_RespectsHintOverride()
    {
        // Force Mobalytics parsing on the Maxroll fixture via hint
        Should.NotThrow(() =>
            new BuildGuideImporter().Import(MaxrollFixture, BuildGuideFormat.Maxroll));
    }

    // ── Mobalytics parser ────────────────────────────────────────────────────

    [Fact]
    public void Mobalytics_ParsesCorrectSlotCount()
    {
        var guide = new MobalyticsParser().Parse(MobalyticsFixture);
        guide.Slots.Count.ShouldBe(2);
    }

    [Fact]
    public void Mobalytics_ParsesSlotLabels()
    {
        var guide = new MobalyticsParser().Parse(MobalyticsFixture);
        guide.Slots[0].SlotLabel.ShouldBe("Helm");
        guide.Slots[1].SlotLabel.ShouldBe("Chest armor");
    }

    [Fact]
    public void Mobalytics_ParsesItemName()
    {
        var guide = new MobalyticsParser().Parse(MobalyticsFixture);
        guide.Slots[0].ItemName.ShouldBe("Harlequin Crest");
    }

    [Fact]
    public void Mobalytics_ParsesAffixPriorities()
    {
        var guide = new MobalyticsParser().Parse(MobalyticsFixture);
        var affixes = guide.Slots[0].Affixes;
        affixes.Count.ShouldBe(4);
        affixes[0].Priority.ShouldBe(1);
        affixes[0].RawName.ShouldBe("Lucky Hit: Up to a 15% Chance to Restore Primary Resource");
        affixes[1].Priority.ShouldBe(2);
        affixes[1].RawName.ShouldBe("Critical Strike Chance");
        affixes[2].Priority.ShouldBe(3);
        affixes[2].RawName.ShouldBe("Attack Speed");
        affixes[3].Priority.ShouldBe(4);
        affixes[3].RawName.ShouldBe("Dexterity");
    }

    [Fact]
    public void Mobalytics_SkipsPriority5AffixSlot()
    {
        // Priority 5 is the aspect/unique imprint slot — no affix name follows
        var guide = new MobalyticsParser().Parse(MobalyticsFixture);
        guide.Slots[0].Affixes.ShouldAllBe(a => a.Priority != 5);
    }

    [Fact]
    public void Mobalytics_ExcludesTemperLines()
    {
        var guide = new MobalyticsParser().Parse(MobalyticsFixture);
        guide.Slots.ShouldAllBe(s =>
            s.Affixes.All(a => !a.RawName.Contains("Tempering") && !a.RawName.Contains("(")));
    }

    [Fact]
    public void Mobalytics_ExcludesSocketContent()
    {
        var guide = new MobalyticsParser().Parse(MobalyticsFixture);
        guide.Slots.ShouldAllBe(s =>
            s.Affixes.All(a => !a.RawName.Contains("Skull") && !a.RawName.Contains("Ruby")));
    }

    // ── Maxroll parser ───────────────────────────────────────────────────────

    [Fact]
    public void Maxroll_ParsesCorrectSlotCount()
    {
        // Seal produces one talisman slot (not counted per-charm), unique produces one slot
        var guide = new MaxrollParser().Parse(MaxrollFixture);
        guide.Slots.Count.ShouldBe(3); // Helm (unique), Chest Armor, Seal
    }

    [Fact]
    public void Maxroll_ParsesUniqueViaSentinel()
    {
        var guide = new MaxrollParser().Parse(MaxrollFixture);
        var helm = guide.Slots.First(s => s.SlotLabel == "Helm");
        helm.HasUniqueSentinel.ShouldBeTrue();
        helm.ItemName.ShouldBe("Harlequin Crest");
    }

    [Fact]
    public void Maxroll_DiscardsBonusLinesAfterUniqueSentinel()
    {
        var guide = new MaxrollParser().Parse(MaxrollFixture);
        var helm = guide.Slots.First(s => s.SlotLabel == "Helm");
        // Unique Effect bonus line "+2 to All Skills" must not appear as an affix
        helm.Affixes.ShouldAllBe(a => !a.RawName.Contains("+2 to All Skills"));
    }

    [Fact]
    public void Maxroll_ParsesGreaterAffixSuffix()
    {
        var guide = new MaxrollParser().Parse(MaxrollFixture);
        var chest = guide.Slots.First(s => s.SlotLabel == "Chest Armor");
        var gaAffix = chest.Affixes.FirstOrDefault(a => a.IsGreaterAffix);
        gaAffix.ShouldNotBeNull();
        gaAffix.RawName.ShouldBe("Maximum Life");
    }

    [Fact]
    public void Maxroll_StripsXPrefix()
    {
        var guide = new MaxrollParser().Parse(MaxrollFixture);
        var chest = guide.Slots.First(s => s.SlotLabel == "Chest Armor");
        chest.Affixes.ShouldAllBe(a => !a.RawName.StartsWith("x") && !a.RawName.StartsWith("X"));
    }

    [Fact]
    public void Maxroll_MarksTalismanSlot()
    {
        var guide = new MaxrollParser().Parse(MaxrollFixture);
        var seal = guide.Slots.First(s => s.IsTalismanSlot);
        seal.SlotLabel.ShouldBe("Seal");
    }

    // ── Icy Veins parser ─────────────────────────────────────────────────────

    [Fact]
    public void IcyVeins_ParsesCorrectSlotCount()
    {
        var guide = new IcyVeinsParser().Parse(IcyVeinsFixture);
        guide.Slots.Count.ShouldBe(2);
    }

    [Fact]
    public void IcyVeins_ParsesSlotLabels()
    {
        var guide = new IcyVeinsParser().Parse(IcyVeinsFixture);
        guide.Slots[0].SlotLabel.ShouldBe("Helm");
        guide.Slots[1].SlotLabel.ShouldBe("Chest");
    }

    [Fact]
    public void IcyVeins_ParsesFourAffixesPerSlot()
    {
        var guide = new IcyVeinsParser().Parse(IcyVeinsFixture);
        guide.Slots[0].Affixes.Count.ShouldBe(4);
        guide.Slots[1].Affixes.Count.ShouldBe(4);
    }

    [Fact]
    public void IcyVeins_ParsesAffixNamesWithoutNumberPrefix()
    {
        var guide = new IcyVeinsParser().Parse(IcyVeinsFixture);
        var affixes = guide.Slots[0].Affixes;
        affixes[0].RawName.ShouldBe("Critical Strike Chance");
        affixes[1].RawName.ShouldBe("Attack Speed");
        affixes[2].RawName.ShouldBe("Dexterity");
        affixes[3].RawName.ShouldBe("Movement Speed");
    }

    [Fact]
    public void IcyVeins_AssignsExplicitPriorities()
    {
        var guide = new IcyVeinsParser().Parse(IcyVeinsFixture);
        var affixes = guide.Slots[0].Affixes;
        for (var i = 0; i < affixes.Count; i++)
            affixes[i].Priority.ShouldBe(i + 1);
    }

    [Fact]
    public void IcyVeins_StripsTemperColumn()
    {
        var guide = new IcyVeinsParser().Parse(IcyVeinsFixture);
        guide.Slots.ShouldAllBe(s =>
            s.Affixes.All(a => !a.RawName.Contains("Category") && !a.RawName.Contains("(Name)")));
    }

    // ── Icy Veins browser multi-line cell paste ──────────────────────────────

    [Fact]
    public void IcyVeins_CrlfLineEndings_ParsesCorrectSlotCount()
    {
        var crlfFixture = IcyVeinsFixture.ReplaceLineEndings("\r\n");
        var guide = new IcyVeinsParser().Parse(crlfFixture);
        guide.Slots.Count.ShouldBe(2);
    }

    [Fact]
    public void IcyVeins_SlotNameOnOwnLine_ParsesCorrectSlotCount()
    {
        // Some browsers paste the slot name and first affix on separate lines (no tab between them)
        var guide = new IcyVeinsParser().Parse(IcyVeinsSeparateLineFixture);
        guide.Slots.Count.ShouldBe(2);
    }

    [Fact]
    public void IcyVeins_BrowserPaste_ParsesCorrectSlotCount()
    {
        var guide = new IcyVeinsParser().Parse(IcyVeinsBrowserPasteFixture);
        guide.Slots.Count.ShouldBe(2);
    }

    [Fact]
    public void IcyVeins_BrowserPaste_ParsesFourAffixesPerSlot()
    {
        var guide = new IcyVeinsParser().Parse(IcyVeinsBrowserPasteFixture);
        guide.Slots[0].Affixes.Count.ShouldBe(4);
        guide.Slots[1].Affixes.Count.ShouldBe(4);
    }

    [Fact]
    public void IcyVeins_BrowserPaste_ParsesAffixNamesCorrectly()
    {
        var guide = new IcyVeinsParser().Parse(IcyVeinsBrowserPasteFixture);
        var affixes = guide.Slots[0].Affixes;
        affixes[0].RawName.ShouldBe("Critical Strike Chance");
        affixes[1].RawName.ShouldBe("Attack Speed");
        affixes[2].RawName.ShouldBe("Dexterity");
        affixes[3].RawName.ShouldBe("Movement Speed");
    }

    [Fact]
    public void IcyVeins_BrowserPaste_IgnoresTemperColumn()
    {
        var guide = new IcyVeinsParser().Parse(IcyVeinsBrowserPasteFixture);
        guide.Slots.ShouldAllBe(s =>
            s.Affixes.All(a => !a.RawName.Contains("Category") && !a.RawName.Contains("(Name)")));
    }

    // ── Static fixtures ──────────────────────────────────────────────────────

    private const string MobalyticsFixture = """
        1
        Helm
        Harlequin Crest
        toggle modifiers
        1
        Lucky Hit: Up to a 15% Chance to Restore Primary Resource
        2
        Critical Strike Chance
        3
        Attack Speed
        4
        Dexterity
        5

        Tempering: Weaponmaster's Tempering (Combat)
        6
        Skull x32% Physical Damage Multiplier

        2
        Chest armor
        Ancient's Grasp
        toggle modifiers
        1
        Damage Reduction
        2
        Maximum Life
        3
        Armor
        4
        Strength
        5

        Tempering: Armored Hide (Defensive)
        7
        Ruby x18% Fire Resistance
        """;

    private const string MaxrollFixture = """
        Helm
        Harlequin Crest
        Lucky Hit Chance
        Cooldown Reduction
        Unique Effect
        +2 to All Skills
        Chest Armor
        Crackling Aura
        Maximum Life↑
        xArmor
        Damage Reduction
        Seal
        Sacred Charm
        some stat here
        """;

    private const string IcyVeinsFixture =
        "Slot\tGear Affixes\tTempering Affixes\n" +
        "Helm\t1. Critical Strike Chance\t+ Category (Name)\n" +
        "2. Attack Speed\n" +
        "3. Dexterity\n" +
        "4. Movement Speed\t+ Category (Name)\n" +
        "Chest\t1. Damage Reduction\t+ Category (Name)\n" +
        "2. Maximum Life\n" +
        "3. Armor\n" +
        "4. Strength\t+ Category (Name)\n";

    // Slot name on its own line (no tab between slot and first affix — some browser/OS combinations).
    private const string IcyVeinsSeparateLineFixture =
        "Slot\tGear Affixes\tTempering Affixes\n" +
        "Helm\n" +
        "1. Critical Strike Chance\n" +
        "2. Attack Speed\n" +
        "3. Dexterity\n" +
        "4. Movement Speed\n" +
        "Chest\n" +
        "1. Damage Reduction\n" +
        "2. Maximum Life\n" +
        "3. Armor\n" +
        "4. Strength\n";

    // Browser multi-line cell paste: each affix on its own row with empty first column (leading tab).
    // Tempering affixes appear as a separate row with two leading tabs.
    private const string IcyVeinsBrowserPasteFixture =
        "Slot\tGear Affixes\tTempering Affixes\n" +
        "Helm\t1. Critical Strike Chance\t\n" +
        "\t2. Attack Speed\t\n" +
        "\t3. Dexterity\t\n" +
        "\t4. Movement Speed\t\n" +
        "\t\t+ Category (Name)\n" +
        "Chest\t1. Damage Reduction\t\n" +
        "\t2. Maximum Life\t\n" +
        "\t3. Armor\t\n" +
        "\t4. Strength\t\n" +
        "\t\t+ Category (Name)\n";
}

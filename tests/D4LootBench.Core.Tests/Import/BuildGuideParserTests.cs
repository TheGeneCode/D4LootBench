using D4LootBench.Core.Data;
using D4LootBench.Core.Import;
using Shouldly;

namespace D4LootBench.Core.Tests.Import;

public sealed class BuildGuideParserTests
{
    private static NameResolver NewResolver() => new(new FilterDataService());

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

    [Fact]
    public void Maxroll_BarbarianTwoHandWeaponHeaders_DelimitDistinctSlots()
    {
        // Regression: "Bludgeoning Weapon"/"Slicing Weapon" are the Maxroll labels for a Barbarian's
        // two-handed arsenal slots. They must delimit their own slots (GoalBuildFactory then routes them
        // to the Bludgeoning/Slicing roles → Two-Handed item types). Previously they were not in the
        // slot-keyword set, so they were absorbed as affix lines of the preceding armor slot and no
        // weapon slot was emitted at all.
        const string paste = """
            Boots
            Movement Speed
            Strength
            Bludgeoning Weapon
            Strength
            Critical Strike Damage
            Slicing Weapon
            Dexterity
            Vulnerable Damage
            """;
        var guide = new MaxrollParser(NewResolver()).Parse(paste);

        guide.Slots.Select(s => s.SlotLabel).ShouldBe(["Boots", "Bludgeoning Weapon", "Slicing Weapon"]);
        // The weapon headers did not leak into the preceding armor slot's affix list.
        guide.Slots.Single(s => s.SlotLabel == "Boots")
            .Affixes.ShouldNotContain(a => a.RawName.Contains("Weapon"));
    }

    [Fact]
    public void Maxroll_DropsTrailingTemperedAffix()
    {
        // Maxroll lists the tempered affix as the last positional line; a drop never pre-rolls it, so
        // the parser excludes it from the target set — three affix lines yield two targets.
        const string paste = """
            Boots
            Movement Speed
            Strength
            Maximum Life
            """;
        var guide = new MaxrollParser(NewResolver()).Parse(paste);

        var boots = guide.Slots.Single(s => s.SlotLabel == "Boots");
        boots.Affixes.Select(a => a.RawName).ShouldBe(["Movement Speed", "Strength"]);
        boots.Affixes.ShouldNotContain(a => a.RawName == "Maximum Life");
    }

    [Fact]
    public void Maxroll_SingleAffixSlot_KeepsIt()
    {
        // The sole-affix guard prevents dropping a one-line slot's only target.
        const string paste = """
            Boots
            Movement Speed
            """;
        var guide = new MaxrollParser(NewResolver()).Parse(paste);

        var boots = guide.Slots.Single(s => s.SlotLabel == "Boots");
        boots.Affixes.Count.ShouldBe(1);
        boots.Affixes[0].RawName.ShouldBe("Movement Speed");
    }

    [Fact]
    public void Maxroll_TalismanSlot_Unaffected()
    {
        // A Seal talisman slot is emitted with an empty affix list directly (never via EmitSlot's
        // temper strip), so the drop logic must neither touch it nor error on its empty list.
        var guide = new MaxrollParser().Parse(MaxrollFixture);

        var seal = guide.Slots.Single(s => s.IsTalismanSlot);
        seal.SlotLabel.ShouldBe("Seal");
        seal.Affixes.ShouldBeEmpty();
    }

    // ── Maxroll: trailing-drop x Unique Effect sentinel interaction ──────────

    [Fact]
    public void Maxroll_UniqueSentinel_TwoAffixesBeforeSentinel_DropsLastPrecedingAffix()
    {
        // EmitSlot only fires on the NEXT slot keyword (or EOF); the "Unique Effect" sentinel just
        // switches state to UniqueBonus and discards the bonus-description lines. The affix list the
        // temper-drop guard sees at EmitSlot time is therefore the same list gathered before the
        // sentinel — the drop reaches back through the sentinel and removes the line immediately
        // preceding "Unique Effect", not a "last line of the whole slot" concept. This documents that
        // current, easy-to-miss behavior for a unique with two pre-sentinel affix lines.
        const string paste = """
            Helm
            Harlequin Crest
            Lucky Hit Chance
            Cooldown Reduction
            Unique Effect
            +2 to All Skills
            Chest Armor
            Maximum Life
            """;
        var guide = new MaxrollParser(NewResolver()).Parse(paste);

        var helm = guide.Slots.Single(s => s.SlotLabel == "Helm");
        helm.HasUniqueSentinel.ShouldBeTrue();
        helm.Affixes.Select(a => a.RawName).ShouldBe(["Lucky Hit Chance"]);
        helm.Affixes.ShouldNotContain(a => a.RawName == "Cooldown Reduction");
    }

    [Fact]
    public void Maxroll_UniqueSentinel_SingleAffixBeforeSentinel_GuardKeepsSoleAffix()
    {
        // Sole-affix guard applies identically on the unique path: a unique with exactly one
        // pre-sentinel affix line must not lose its only target.
        const string paste = """
            Helm
            Harlequin Crest
            Lucky Hit Chance
            Unique Effect
            +2 to All Skills
            Chest Armor
            Maximum Life
            """;
        var guide = new MaxrollParser(NewResolver()).Parse(paste);

        var helm = guide.Slots.Single(s => s.SlotLabel == "Helm");
        helm.HasUniqueSentinel.ShouldBeTrue();
        helm.Affixes.Select(a => a.RawName).ShouldBe(["Lucky Hit Chance"]);
    }

    [Fact]
    public void Maxroll_UniqueSentinel_NoAffixesBeforeSentinel_EmptyAffixListNoCrash()
    {
        // A unique whose guide entry lists no rollable affixes at all before "Unique Effect" — the
        // count is 0 (not > 1), so the guard is a no-op; must not throw and must yield an empty list.
        const string paste = """
            Helm
            Harlequin Crest
            Unique Effect
            +2 to All Skills
            Chest Armor
            Maximum Life
            """;
        var guide = new MaxrollParser(NewResolver()).Parse(paste);

        var helm = guide.Slots.Single(s => s.SlotLabel == "Helm");
        helm.HasUniqueSentinel.ShouldBeTrue();
        helm.Affixes.ShouldBeEmpty();
    }

    [Fact]
    public void Maxroll_ExactlyTwoAffixLines_DropsToOneTarget()
    {
        // The 1-vs-2 affix boundary for a plain (non-unique) slot: exactly two affix lines is the
        // smallest input that still triggers the drop (count > 1), leaving exactly one target.
        const string paste = """
            Boots
            Movement Speed
            Maximum Life
            """;
        var guide = new MaxrollParser(NewResolver()).Parse(paste);

        var boots = guide.Slots.Single(s => s.SlotLabel == "Boots");
        boots.Affixes.Select(a => a.RawName).ShouldBe(["Movement Speed"]);
    }

    // ── Maxroll: no-item-name (low-level) slots ──────────────────────────────

    [Fact]
    public void Maxroll_WithResolver_NoItemNameSlot_KeepsFirstAffix()
    {
        // Low-level guide: the slot header is followed directly by ranked affixes (no item/aspect name).
        // The top-ranked affix must NOT be swallowed as an item name. The Boots block lists 7 affix
        // lines; the trailing one is the tempered affix and is dropped, leaving 6.
        var guide = new MaxrollParser(NewResolver()).Parse(NoItemNameFixture);

        var boots = guide.Slots.First(s => s.SlotLabel == "Boots");
        boots.ItemName.ShouldBeNull();
        boots.Affixes[0].RawName.ShouldBe("Movement Speed");
        boots.Affixes.Count.ShouldBe(6);
    }

    [Fact]
    public void Maxroll_WithResolver_NamedItemSlot_StillDetectsItemName()
    {
        // A real item/aspect name does not resolve as an affix, so it is still captured as the item name.
        var guide = new MaxrollParser(NewResolver()).Parse(MaxrollFixture);

        var helm = guide.Slots.First(s => s.SlotLabel == "Helm");
        helm.ItemName.ShouldBe("Harlequin Crest");
        helm.Affixes.ShouldContain(a => a.RawName == "Lucky Hit Chance");
    }

    [Fact]
    public void Maxroll_WithoutResolver_PreservesLegacyFirstLineIsItemName()
    {
        // No resolver → legacy behavior: first line after the slot header is always the item name.
        var guide = new MaxrollParser().Parse(NoItemNameFixture);

        var boots = guide.Slots.First(s => s.SlotLabel == "Boots");
        boots.ItemName.ShouldBe("Movement Speed");
    }

    [Fact]
    public void Maxroll_WithResolver_MultiplierValuePrefix_StrippedAndKeptAsAffix()
    {
        // A multiplicative first-line affix "x24% Vulnerable Damage" must have BOTH the 'x' and the
        // rolled value stripped, leaving the affix name — which resolves (catalog "Vulnerable Damage
        // Multiplier"), so it is kept as affix #1 rather than swallowed as the item name.
        const string paste = """
            Boots
            x24% Vulnerable Damage
            Strength
            """;
        var guide = new MaxrollParser(NewResolver()).Parse(paste);

        var boots = guide.Slots.Single(s => s.SlotLabel == "Boots");
        boots.ItemName.ShouldBeNull();
        boots.Affixes[0].RawName.ShouldBe("Vulnerable Damage");
        NewResolver().IsKnownAffixPhrase("Vulnerable Damage").ShouldBeTrue();
    }

    [Fact]
    public void Maxroll_WithResolver_CatalogAbsentAffix_FallsBackToItemName()
    {
        // DATA GAP (not a parser bug): purely conditional damage multipliers like "Damage to Distant
        // Enemies" are absent from d4-data.json (catalog v5 added the elemental "<Element> Damage
        // Multiplier" family, but not the target/state-conditional ones). Even after the value prefix
        // is stripped the name resolves to nothing, so the first line is treated as the item name.
        // Tracked in docs/design/data-gaps.md.
        var guide = new MaxrollParser(NewResolver()).Parse(NoItemNameFixture);

        var amulet = guide.Slots.First(s => s.SlotLabel == "Amulet");
        amulet.ItemName.ShouldBe("x24% Damage to Distant Enemies");
        amulet.Affixes.ShouldNotContain(a => a.RawName.Contains("Distant"));
    }

    [Fact]
    public void Maxroll_WithResolver_ItemNameContainingAffixSubstring_KeptAsItemName()
    {
        // An item/aspect name that merely contains a catalog affix word ("Strength" inside
        // "Girdle of Boundless Strength") must NOT be misclassified as affix #1. LooksLikeAffix uses
        // NameResolver.IsKnownAffixPhrase's whole-phrase identity check, so the name is kept and the
        // real first affix follows it. See NameResolverTests.IsKnownAffixPhrase_ItemNameContainingAffixSubstring_ReturnsFalse.
        const string paste = """
            Helm
            Girdle of Boundless Strength
            Critical Strike Chance
            Maximum Life
            """;
        var guide = new MaxrollParser(NewResolver()).Parse(paste);

        var helm = guide.Slots.Single(s => s.SlotLabel == "Helm");
        helm.ItemName.ShouldBe("Girdle of Boundless Strength");
        helm.Affixes[0].RawName.ShouldBe("Critical Strike Chance");
    }

    [Fact]
    public void Maxroll_WithResolver_FirstLineHasXPrefixAndGreaterAffixSuffix_ClassifiesAsAffixWithModifiersStripped()
    {
        // The first line after a slot header can itself carry BOTH modifiers at once. LooksLikeAffix
        // must strip them before resolving (StripAffixModifiers runs first), and AddAffix must strip
        // them again independently (it receives the original raw line, not the already-stripped one)
        // so the emitted affix carries the clean name and the correct greater-affix flag.
        const string paste = """
            Boots
            xMovement Speed↑
            Strength
            """;
        var guide = new MaxrollParser(NewResolver()).Parse(paste);

        var boots = guide.Slots.Single(s => s.SlotLabel == "Boots");
        boots.ItemName.ShouldBeNull();
        boots.Affixes[0].RawName.ShouldBe("Movement Speed");
        boots.Affixes[0].IsGreaterAffix.ShouldBeTrue();
    }

    [Fact]
    public void Maxroll_WithResolver_BlankLineAfterSlotHeader_SkippedNotTreatedAsContent()
    {
        // Blank lines are elided by the line-reading loop before the state machine ever sees them, so
        // a blank line directly after a slot header must not itself become the "first line" — the
        // next real content line does, and normal item-name-vs-affix classification still applies.
        const string paste = "Boots\n\nMovement Speed\nStrength";
        var guide = new MaxrollParser(NewResolver()).Parse(paste);

        var boots = guide.Slots.Single(s => s.SlotLabel == "Boots");
        boots.ItemName.ShouldBeNull();
        boots.Affixes[0].RawName.ShouldBe("Movement Speed");
    }

    [Fact]
    public void Maxroll_WithResolver_ItemNameAndUniqueSentinel_BothStillResolve()
    {
        // A slot with a real (non-affix-colliding) item/aspect name followed by the "Unique Effect"
        // sentinel must keep both: the resolver must not interfere with unique detection.
        var guide = new MaxrollParser(NewResolver()).Parse(MaxrollFixture);

        var helm = guide.Slots.First(s => s.SlotLabel == "Helm");
        helm.ItemName.ShouldBe("Harlequin Crest");
        helm.HasUniqueSentinel.ShouldBeTrue();
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

    // Low-level Maxroll paste: slot headers followed directly by ranked affixes, no item/aspect names.
    private const string NoItemNameFixture = """
        Boots
        Movement Speed
        Strength
        Maximum Life
        Fury Regeneration
        Armor
        Resistance to All Elements
        Movement Speed
        Amulet
        x24% Damage to Distant Enemies
        Strength
        Maximum Life
        Critical Strike Chance
        Damage to Close Enemies
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

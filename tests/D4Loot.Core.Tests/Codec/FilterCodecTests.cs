using D4Loot.Core.Codec;
using D4Loot.Core.Data;
using D4Loot.Core.Models;
using Shouldly;

namespace D4Loot.Core.Tests.Codec;

public sealed class FilterCodecTests
{
    // ── Round-trip ────────────────────────────────────────────────────────

    [Fact]
    public void Encode_ThenDecode_PreservesFilterName()
    {
        var ruleset = SimpleRuleset("My Test Filter");
        FilterCodec.Decode(FilterCodec.Encode(ruleset)).Name.ShouldBe("My Test Filter");
    }

    [Fact]
    public void Encode_ThenDecode_PreservesRuleCount()
    {
        FilterCodec.Decode(FilterCodec.Encode(SimpleRuleset("Test"))).Rules.Count.ShouldBe(2);
    }

    [Fact]
    public void Encode_ThenDecode_PreservesRuleFields()
    {
        var rule = new FilterRule(
            "Hide Junk",
            Visibility.HideAll,
            FilterColors.Blue,
            [new RarityCondition(RarityFlags.Common | RarityFlags.Magic | RarityFlags.Rare)]
        );
        var decoded = FilterCodec.Decode(FilterCodec.Encode(new FilterRuleset("Test", [rule]))).Rules[0];

        decoded.Name.ShouldBe("Hide Junk");
        decoded.Visibility.ShouldBe(Visibility.HideAll);
        decoded.Color.ShouldBe(FilterColors.Blue);
        decoded.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Encode_ThenDecode_PreservesRarityCondition()
    {
        const RarityFlags mask = RarityFlags.Rare | RarityFlags.Legendary | RarityFlags.Unique;
        var decoded = RoundTripRule(new FilterRule("Test", Visibility.Show, FilterColors.Blue,
            [new RarityCondition(mask)]));

        decoded.Conditions.Count.ShouldBe(1);
        decoded.Conditions[0].ShouldBeOfType<RarityCondition>().Mask.ShouldBe(mask);
    }

    [Fact]
    public void Encode_ThenDecode_PreservesGreaterAffixCondition()
    {
        var decoded = RoundTripRule(new FilterRule("Test", Visibility.Recolor, FilterColors.Cyan,
            [new GreaterAffixCondition(2)]));

        decoded.Conditions[0].ShouldBeOfType<GreaterAffixCondition>().MinimumCount.ShouldBe(2);
    }

    [Fact]
    public void Encode_ThenDecode_PreservesCodexCondition()
    {
        var decoded = RoundTripRule(new FilterRule("Test", Visibility.Recolor, FilterColors.Green,
            [new CodexCondition()]));

        decoded.Conditions[0].ShouldBeOfType<CodexCondition>();
    }

    [Fact]
    public void Encode_ThenDecode_PreservesAffixConditionIdsAndMinimum()
    {
        uint[] affixIds = [AffixDatabase.ByName["Critical Strike Chance"], AffixDatabase.ByName["Attack Speed"]];
        var decoded = RoundTripRule(new FilterRule("BiS Rare", Visibility.Recolor, FilterColors.Gold,
            [new AffixCondition(affixIds, 2)]));

        var result = decoded.Conditions[0].ShouldBeOfType<AffixCondition>();
        result.MinimumCount.ShouldBe(2);
        result.AffixIds.ShouldBe(affixIds);
    }

    [Fact]
    public void Encode_ThenDecode_PreservesItemTypeCondition()
    {
        uint[] typeIds = [0x00237e80u, 0x0022ed05u];
        var decoded = RoundTripRule(new FilterRule("Talismans", Visibility.Show, FilterColors.Blue,
            [new ItemTypeCondition(typeIds)]));

        decoded.Conditions[0].ShouldBeOfType<ItemTypeCondition>().TypeIds.ShouldBe(typeIds);
    }

    [Fact]
    public void Encode_ThenDecode_PreservesMultipleConditionsPerRule()
    {
        var conditions = new Condition[]
        {
            new RarityCondition(RarityFlags.Rare),
            new AffixCondition([AffixDatabase.ByName["All Damage Multiplier"]], 1)
        };
        RoundTripRule(new FilterRule("Orange Rare", Visibility.Recolor, FilterColors.Orange,
            conditions)).Conditions.Count.ShouldBe(2);
    }

    [Fact]
    public void Encode_ThenDecode_PreservesItemPowerCondition()
    {
        var decoded = RoundTripRule(new FilterRule("Test", Visibility.Show, FilterColors.Blue,
            [new ItemPowerCondition(700, 925)]));

        var result = decoded.Conditions[0].ShouldBeOfType<ItemPowerCondition>();
        result.Minimum.ShouldBe(700);
        result.Maximum.ShouldBe(925);
    }

    [Fact]
    public void Encode_ThenDecode_PreservesItemPropertiesCondition()
    {
        var decoded = RoundTripRule(new FilterRule("Test", Visibility.Show, FilterColors.Blue,
            [new ItemPropertiesCondition(4)]));

        decoded.Conditions[0].ShouldBeOfType<ItemPropertiesCondition>().PropertyMask.ShouldBe(4);
    }

    [Fact]
    public void Encode_ThenDecode_PreservesOptionalAffixCondition()
    {
        uint[] affixIds = [AffixDatabase.ByName["Critical Strike Chance"], AffixDatabase.ByName["Maximum Life"]];
        var decoded = RoundTripRule(new FilterRule("Test", Visibility.Show, FilterColors.Blue,
            [new OptionalAffixCondition(affixIds)]));

        decoded.Conditions[0].ShouldBeOfType<OptionalAffixCondition>().AffixIds.ShouldBe(affixIds);
    }

    // ── Encode produces valid Base64 ──────────────────────────────────────

    [Fact]
    public void Encode_ProducesValidBase64()
    {
        var code = FilterCodec.Encode(SimpleRuleset("Test"));
        Should.NotThrow(() => Convert.FromBase64String(code));
    }

    // ── Decode is tolerant of whitespace ─────────────────────────────────

    [Fact]
    public void Decode_StripsWhitespaceFromShareCode()
    {
        var code = FilterCodec.Encode(SimpleRuleset("Test"));
        Should.NotThrow(() => FilterCodec.Decode("  " + code + "\n"));
    }

    // ── Encode → decode → encode produces identical bytes ────────────────

    [Fact]
    public void EncodeIsIdempotent_AfterRoundTrip()
    {
        var original = BuildGenericCritRuleset();
        var code1 = FilterCodec.Encode(original);
        FilterCodec.Encode(FilterCodec.Decode(code1)).ShouldBe(code1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static FilterRule RoundTripRule(FilterRule rule)
        => FilterCodec.Decode(FilterCodec.Encode(new FilterRuleset("T", [rule]))).Rules[0];

    private static FilterRuleset SimpleRuleset(string name) => new(name,
    [
        new FilterRule("Show All",  Visibility.Show,    FilterColors.Blue, [new RarityCondition(RarityFlags.All)]),
        new FilterRule("Hide Junk", Visibility.HideAll, FilterColors.Blue, [new RarityCondition(RarityFlags.Common | RarityFlags.Magic)])
    ]);

    private static FilterRuleset BuildGenericCritRuleset()
    {
        uint[] coreIds =
        [
            AffixDatabase.ByName["Critical Strike Chance"],
            AffixDatabase.ByName["Critical Strike Damage Multiplier"],
            AffixDatabase.ByName["Attack Speed"],
            AffixDatabase.ByName["All Damage Multiplier"],
            AffixDatabase.ByName["Vulnerable Damage Multiplier"],
        ];
        uint[] secIds =
        [
            AffixDatabase.ByName["Maximum Life"],
            AffixDatabase.ByName["Armor"],
        ];
        var allIds = coreIds.Concat(secIds).ToArray();

        return new FilterRuleset("Generic Crit",
        [
            new FilterRule("Legendary Talismans",      Visibility.Show,    FilterColors.Blue,
                [new RarityCondition(RarityFlags.LegendaryPlus), new ItemTypeCondition([0x00237e80, 0x0022ed05])]),
            new FilterRule("Hide Junk",                Visibility.HideAll, FilterColors.Blue,
                [new RarityCondition(RarityFlags.Common | RarityFlags.Magic | RarityFlags.Rare)]),
            new FilterRule("Check Rare - Build Affix", Visibility.Recolor, FilterColors.Orange,
                [new RarityCondition(RarityFlags.Rare), new AffixCondition(allIds, 1)]),
            new FilterRule("BiS Rare - 2+ Core Stats", Visibility.Recolor, FilterColors.Gold,
                [new RarityCondition(RarityFlags.Rare), new AffixCondition(coreIds, 2)]),
            new FilterRule("Codex Upgrade",            Visibility.Recolor, FilterColors.Green,
                [new CodexCondition()]),
            new FilterRule("Legendaries - Keep All",   Visibility.Recolor, FilterColors.Green,
                [new RarityCondition(RarityFlags.LegendaryPlus)]),
            new FilterRule("Greater Affix - Loot",     Visibility.Recolor, FilterColors.Cyan,
                [new GreaterAffixCondition(1)]),
            new FilterRule("Show All - Catch All",     Visibility.Show,    FilterColors.Blue,
                [new RarityCondition(RarityFlags.All)]),
        ]);
    }
}

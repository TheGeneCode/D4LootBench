using System.Text.Json;
using D4LootBench.Core.Data;
using D4LootBench.Core.Models;
using D4LootBench.Core.Serialization;
using Shouldly;

namespace D4LootBench.Core.Tests.SerializationTests;

public class AnnotatedJsonTests
{
    [Fact]
    public void Serialize_AffixCondition_EmitsAnnotatedShape()
    {
        var cdr = AffixDatabase.ByName["%Cooldown Reduction"].Hash;
        var armor = AffixDatabase.ByName["+Armor"].Hash;
        var cond  = new AffixCondition([cdr, armor], 2)
        {
            GreaterEntries = [new GreaterAffixEntry(cdr, cdr)],
        };
        var json = JsonSerializer.Serialize<Condition>(cond, FilterJsonOptions.Default);

        json.ShouldContain("\"id\": \"0x001beab8\"");
        json.ShouldContain("\"name\": \"%Cooldown Reduction\"");
        json.ShouldContain("\"name\": \"+Armor\"");
        json.ShouldContain("\"affixId\": \"0x001beab8\"");
        json.ShouldContain("\"affixName\": \"%Cooldown Reduction\"");
        json.ShouldContain("\"affixIdEcho\": \"0x001beab8\"");
    }

    [Fact]
    public void Deserialize_AcceptsLegacyStringHashForm()
    {
        const string json = """{"$type":"affix","AffixIds":["0x001beab8","0x001beab2"],"MinimumCount":2}""";
        var c = JsonSerializer.Deserialize<Condition>(json, FilterJsonOptions.Default).ShouldBeOfType<AffixCondition>();
        c.AffixIds.Count.ShouldBe(2);
        c.AffixIds[0].ShouldBe(0x001beab8u);
        c.AffixIds[1].ShouldBe(0x001beab2u);
    }

    [Fact]
    public void Deserialize_AcceptsAnnotatedForm_IdWins()
    {
        const string json = """
        {
          "$type":"affix",
          "AffixIds":[
            {"id":"0x001beab8","name":"%Cooldown Reduction"},
            {"id":"0x001beab2","name":"+Armor"}
          ],
          "MinimumCount":2
        }
        """;
        var c = JsonSerializer.Deserialize<Condition>(json, FilterJsonOptions.Default).ShouldBeOfType<AffixCondition>();
        c.AffixIds[0].ShouldBe(0x001beab8u);
        c.AffixIds[1].ShouldBe(0x001beab2u);
    }

    [Fact]
    public void Deserialize_NameOnlyResolvesViaCatalog()
    {
        const string json = """
        {
          "$type":"affix",
          "AffixIds":[
            {"name":"%Cooldown Reduction"},
            {"name":"+Armor"}
          ],
          "MinimumCount":2
        }
        """;
        var c = JsonSerializer.Deserialize<Condition>(json, FilterJsonOptions.Default).ShouldBeOfType<AffixCondition>();
        c.AffixIds[0].ShouldBe(0x001beab8u);
        c.AffixIds[1].ShouldBe(0x001beab2u);
    }

    [Fact]
    public void Serialize_UnknownHash_ProducesEmptyName_RoundTrips()
    {
        const uint phantom = 0xDEADBEEFu;
        var cond = new AffixCondition([phantom], 1);
        var json = JsonSerializer.Serialize<Condition>(cond, FilterJsonOptions.Default);

        json.ShouldContain("\"id\": \"0xdeadbeef\"");
        // Affix catalog GetDisplayName falls back to "Unknown (0x...)" — but the value is preserved
        var roundTripped = JsonSerializer.Deserialize<Condition>(json, FilterJsonOptions.Default).ShouldBeOfType<AffixCondition>();
        roundTripped.AffixIds.Single().ShouldBe(phantom);
    }

    [Fact]
    public void AnnotatedRoundTrip_AllConditionsTestFixture()
    {
        var path    = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                                   "json-filters", "All Conditions Test.json");
        var json    = File.ReadAllText(Path.GetFullPath(path));
        var ruleset = JsonSerializer.Deserialize<FilterRuleset>(json, FilterJsonOptions.Default)!;
        var json2   = JsonSerializer.Serialize(ruleset, FilterJsonOptions.Default);
        var ruleset2 = JsonSerializer.Deserialize<FilterRuleset>(json2, FilterJsonOptions.Default)!;
        ruleset2.Rules.Count.ShouldBe(ruleset.Rules.Count);
    }
}

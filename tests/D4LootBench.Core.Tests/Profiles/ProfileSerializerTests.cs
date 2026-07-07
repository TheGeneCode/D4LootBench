using System.Text.Json;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
using D4LootBench.Core.Profiles;
using Shouldly;

namespace D4LootBench.Core.Tests.Profiles;

public sealed class ProfileSerializerTests
{
    private static ProgressionProfile Sample(string name = "Barb Bash") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        PlayerClass = PlayerClass.Barbarian,
        GuideFormat = BuildGuideFormat.Maxroll,
        GuideText = "Helm\nCritical Strike Chance\n",
        Gear =
        [
            new GearItem
            {
                Slot = GearSlot.Helm,
                ItemTypeName = "Helm",
                ItemPower = 925,
                Rarity = ItemRarity.Legendary,
                IsAncestral = true,
                UniqueHash = 0x00abc123,
                Affixes =
                [
                    new GearAffix { RawText = "+45.0% Critical Strike Chance", ResolvedName = "Critical Strike Chance", AffixHash = 0x001d5d01, IsGreaterAffix = true },
                    new GearAffix { RawText = "garbled ocr line", ResolvedName = null, AffixHash = null, IsGreaterAffix = false },
                ],
            },
        ],
    };

    [Fact]
    public void RoundTrip_FullProfile_PreservesAllFields()
    {
        var original = Sample();

        var round = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        round.Id.ShouldBe(original.Id);
        round.Name.ShouldBe(original.Name);
        round.CreatedUtc.ShouldBe(original.CreatedUtc);
        round.ModifiedUtc.ShouldBe(original.ModifiedUtc);
        round.PlayerClass.ShouldBe(PlayerClass.Barbarian);
        round.GuideFormat.ShouldBe(BuildGuideFormat.Maxroll);
        round.GuideText.ShouldBe("Helm\nCritical Strike Chance\n");

        round.Gear.Count.ShouldBe(1);
        var gear = round.Gear[0];
        gear.Slot.ShouldBe(GearSlot.Helm);
        gear.ItemTypeName.ShouldBe("Helm");
        gear.ItemPower.ShouldBe(925);
        gear.Rarity.ShouldBe(ItemRarity.Legendary);
        gear.IsAncestral.ShouldBeTrue();
        gear.UniqueHash.ShouldBe(0x00abc123u);

        gear.Affixes.Count.ShouldBe(2);
        gear.Affixes[0].RawText.ShouldBe("+45.0% Critical Strike Chance");
        gear.Affixes[0].ResolvedName.ShouldBe("Critical Strike Chance");
        gear.Affixes[0].AffixHash.ShouldBe(0x001d5d01u);
        gear.Affixes[0].IsGreaterAffix.ShouldBeTrue();
        gear.Affixes[1].RawText.ShouldBe("garbled ocr line");
        gear.Affixes[1].ResolvedName.ShouldBeNull();
        gear.Affixes[1].AffixHash.ShouldBeNull();
        gear.Affixes[1].IsGreaterAffix.ShouldBeFalse();
    }

    [Fact]
    public void Serialize_WritesHexHashesAndStringEnums()
    {
        var json = ProfileSerializer.Serialize(Sample());

        json.ShouldContain("0x001d5d01");
        json.ShouldContain("Barbarian");
        json.ShouldContain("Legendary");
        json.ShouldContain("\"schemaVersion\": 1");
    }

    [Fact]
    public void Deserialize_UnknownField_Ignored()
    {
        var json = ProfileSerializer.Serialize(Sample());
        var withExtra = json.Insert(json.IndexOf('{') + 1, "\"futureField\": 42,");

        Should.NotThrow(() => ProfileSerializer.Deserialize(withExtra));
    }

    [Fact]
    public void Deserialize_Garbage_Throws()
        => Should.Throw<JsonException>(() => ProfileSerializer.Deserialize("not json"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Deserialize_EmptyOrNullOrWrongShapeInput_ThrowsJsonException(string json)
        => Should.Throw<JsonException>(() => ProfileSerializer.Deserialize(json));

    [Fact]
    public void Deserialize_InvalidEnumString_ThrowsJsonException()
    {
        var json = ValidJsonWith("\"playerClass\":\"NotARealClass\"");

        Should.Throw<JsonException>(() => ProfileSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_NumericEnumValue_MapsToOrdinalMember()
    {
        // Documents a JsonStringEnumConverter quirk: it also accepts raw numeric tokens and maps
        // them positionally (1 == PlayerClass.Barbarian) without validating the value is defined.
        var json = ValidJsonWith("\"playerClass\":1");

        var profile = ProfileSerializer.Deserialize(json);

        profile.PlayerClass.ShouldBe(PlayerClass.Barbarian);
    }

    [Fact]
    public void Deserialize_InvalidHexHashString_ThrowsJsonException()
    {
        // Regression test: HexUInt32Converter previously let a raw FormatException escape
        // Deserialize, violating its documented "throws JsonException" contract.
        var json = """
        {"schemaVersion":1,"id":"11111111-1111-1111-1111-111111111111","name":"x","createdUtc":"2026-01-01T00:00:00Z","modifiedUtc":"2026-01-01T00:00:00Z","playerClass":"All","guideFormat":"Auto","guideText":"","gear":[{"slot":"Helm","itemTypeName":null,"itemPower":null,"rarity":"Unknown","isAncestral":false,"uniqueHash":"not-a-hex","affixes":[]}]}
        """;

        Should.Throw<JsonException>(() => ProfileSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_MissingGearAndSchemaVersion_DefaultsGracefully()
    {
        // Simulates a legacy/partial document: no "gear" array and no "schemaVersion" at all.
        var json = """
        {"id":"11111111-1111-1111-1111-111111111111","name":"Legacy","createdUtc":"2026-01-01T00:00:00Z","modifiedUtc":"2026-01-01T00:00:00Z","playerClass":"All","guideFormat":"Auto","guideText":""}
        """;

        var profile = ProfileSerializer.Deserialize(json);

        profile.Name.ShouldBe("Legacy");
        profile.Gear.ShouldBeEmpty();
    }

    [Fact]
    public void Deserialize_ExplicitNullGearArray_DefaultsToEmptyList()
    {
        var json = """
        {"schemaVersion":1,"id":"11111111-1111-1111-1111-111111111111","name":"x","createdUtc":"2026-01-01T00:00:00Z","modifiedUtc":"2026-01-01T00:00:00Z","playerClass":"All","guideFormat":"Auto","guideText":"","gear":null}
        """;

        var profile = ProfileSerializer.Deserialize(json);

        profile.Gear.ShouldBeEmpty();
    }

    [Fact]
    public void Deserialize_GearItemMissingAffixesField_DefaultsToEmptyList()
    {
        var json = """
        {"schemaVersion":1,"id":"11111111-1111-1111-1111-111111111111","name":"x","createdUtc":"2026-01-01T00:00:00Z","modifiedUtc":"2026-01-01T00:00:00Z","playerClass":"All","guideFormat":"Auto","guideText":"","gear":[{"slot":"Helm","rarity":"Unknown","isAncestral":false}]}
        """;

        var profile = ProfileSerializer.Deserialize(json);

        profile.Gear.Count.ShouldBe(1);
        profile.Gear[0].Affixes.ShouldBeEmpty();
    }

    [Fact]
    public void RoundTrip_AllNullableFieldsNull_PreservesNulls()
    {
        var original = new ProgressionProfile
        {
            Id = Guid.NewGuid(),
            Name = "Bare Necessities",
            Gear =
            [
                new GearItem
                {
                    Slot = GearSlot.Ring,
                    ItemTypeName = null,
                    ItemPower = null,
                    Rarity = ItemRarity.Unknown,
                    IsAncestral = false,
                    UniqueHash = null,
                    Affixes = [new GearAffix { RawText = "unreadable", ResolvedName = null, AffixHash = null, IsGreaterAffix = false }],
                },
            ],
        };

        var round = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        round.Gear[0].ItemTypeName.ShouldBeNull();
        round.Gear[0].ItemPower.ShouldBeNull();
        round.Gear[0].UniqueHash.ShouldBeNull();
        round.Gear[0].Affixes[0].ResolvedName.ShouldBeNull();
        round.Gear[0].Affixes[0].AffixHash.ShouldBeNull();
    }

    [Theory]
    [InlineData(0x00000000u)]
    [InlineData(0xFFFFFFFFu)]
    public void RoundTrip_HashBoundaryValues_PreservesUInt32Extremes(uint hash)
    {
        var original = Sample() with
        {
            Gear = [new GearItem { Slot = GearSlot.Weapon, Rarity = ItemRarity.Unique, UniqueHash = hash, Affixes = [] }],
        };

        var round = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        round.Gear[0].UniqueHash.ShouldBe(hash);
    }

    [Fact]
    public void RoundTrip_EmptyGearList_PreservesEmptyList()
    {
        var original = Sample() with { Gear = [] };

        var round = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        round.Gear.ShouldBeEmpty();
    }

    [Fact]
    public void RoundTrip_UnicodeAndMultilineName_PreservesExactText()
    {
        var name = "Bàrb 战士 (⚔) copy\nline two";
        var original = Sample(name) with { GuideText = "Line1\nLine2\r\n日本語" };

        var round = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        round.Name.ShouldBe(name);
        round.GuideText.ShouldBe("Line1\nLine2\r\n日本語");
    }

    private static string ValidJsonWith(string playerClassAssignment)
    {
        var baseJson = """
        {"schemaVersion":1,"id":"11111111-1111-1111-1111-111111111111","name":"x","createdUtc":"2026-01-01T00:00:00Z","modifiedUtc":"2026-01-01T00:00:00Z","playerClass":"All","guideFormat":"Auto","guideText":"","gear":[]}
        """;
        return baseJson.Replace("\"playerClass\":\"All\"", playerClassAssignment);
    }
}

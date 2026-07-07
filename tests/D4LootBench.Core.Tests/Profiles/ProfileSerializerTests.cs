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
        json.ShouldContain("\"schemaVersion\": 2");
    }

    [Fact]
    public void RoundTrip_PreservesBlockCodes()
    {
        var original = Sample() with { OverrideBlockCode = "AAAA", OverriddenByBlockCode = "BBBB" };

        var round = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        round.OverrideBlockCode.ShouldBe("AAAA");
        round.OverriddenByBlockCode.ShouldBe("BBBB");
    }

    [Fact]
    public void Deserialize_V1FileWithoutBlockFields_DefaultsToNull()
    {
        var json = """
        {"schemaVersion":1,"id":"11111111-1111-1111-1111-111111111111","name":"Legacy V1","createdUtc":"2026-01-01T00:00:00Z","modifiedUtc":"2026-01-01T00:00:00Z","playerClass":"All","guideFormat":"Auto","guideText":"","gear":[]}
        """;

        var profile = ProfileSerializer.Deserialize(json);

        profile.OverrideBlockCode.ShouldBeNull();
        profile.OverriddenByBlockCode.ShouldBeNull();
        profile.Name.ShouldBe("Legacy V1");
        profile.Gear.ShouldBeEmpty();
    }

    [Fact]
    public void Serialize_StampsSchemaVersion2()
    {
        var json = ProfileSerializer.Serialize(Sample());

        json.ShouldContain("\"schemaVersion\": 2");
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

    [Fact]
    public void RoundTrip_NullBlockCodes_PreservesNull()
    {
        var original = Sample(); // OverrideBlockCode/OverriddenByBlockCode default to null.

        var round = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        round.OverrideBlockCode.ShouldBeNull();
        round.OverriddenByBlockCode.ShouldBeNull();
    }

    [Fact]
    public void RoundTrip_EmptyStringBlockCodes_PreservesEmptyString_DistinctFromNull()
    {
        var original = Sample() with { OverrideBlockCode = "", OverriddenByBlockCode = null };

        var round = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        round.OverrideBlockCode.ShouldBe("");
        round.OverriddenByBlockCode.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_V2FileWithExplicitNullBlockCodes_DefaultsToNull()
    {
        var json = """
        {"schemaVersion":2,"id":"11111111-1111-1111-1111-111111111111","name":"Explicit Null","createdUtc":"2026-01-01T00:00:00Z","modifiedUtc":"2026-01-01T00:00:00Z","playerClass":"All","guideFormat":"Auto","guideText":"","gear":[],"overrideBlockCode":null,"overriddenByBlockCode":null}
        """;

        var profile = ProfileSerializer.Deserialize(json);

        profile.OverrideBlockCode.ShouldBeNull();
        profile.OverriddenByBlockCode.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_FutureSchemaVersion_IgnoredAndDeserializesFine()
    {
        // SchemaVersion is a write-only marker today — FromStored never branches on it, so an
        // unrecognized future (or garbage) version number must not block loading.
        var json = """
        {"schemaVersion":99,"id":"11111111-1111-1111-1111-111111111111","name":"Future","createdUtc":"2026-01-01T00:00:00Z","modifiedUtc":"2026-01-01T00:00:00Z","playerClass":"All","guideFormat":"Auto","guideText":"","gear":[],"overrideBlockCode":"CODE1","overriddenByBlockCode":"CODE2"}
        """;

        var profile = ProfileSerializer.Deserialize(json);

        profile.Name.ShouldBe("Future");
        profile.OverrideBlockCode.ShouldBe("CODE1");
        profile.OverriddenByBlockCode.ShouldBe("CODE2");
    }

    [Fact]
    public void RoundTrip_LongBlockCode_PreservesExactText()
    {
        var longCode = new string('A', 5000) + "==";
        var original = Sample() with { OverrideBlockCode = longCode };

        var round = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        round.OverrideBlockCode.ShouldBe(longCode);
    }

    [Fact]
    public void RoundTrip_NonBase64GarbageBlockCode_PreservesOpaqueString()
    {
        // ProfileSerializer treats the block codes as opaque strings — it does not validate base64
        // shape (that responsibility belongs to FilterCodec.Decode at consumption time).
        var original = Sample() with { OverrideBlockCode = "not valid base64!! \"quote\" \\backslash" };

        var round = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        round.OverrideBlockCode.ShouldBe("not valid base64!! \"quote\" \\backslash");
    }

    [Fact]
    public void RoundTrip_OneNullOneSetBlockCode_PreservesEachIndependently()
    {
        var original = Sample() with { OverrideBlockCode = null, OverriddenByBlockCode = "ONLY_THIS_ONE==" };

        var round = ProfileSerializer.Deserialize(ProfileSerializer.Serialize(original));

        round.OverrideBlockCode.ShouldBeNull();
        round.OverriddenByBlockCode.ShouldBe("ONLY_THIS_ONE==");
    }

    private static string ValidJsonWith(string playerClassAssignment)
    {
        var baseJson = """
        {"schemaVersion":1,"id":"11111111-1111-1111-1111-111111111111","name":"x","createdUtc":"2026-01-01T00:00:00Z","modifiedUtc":"2026-01-01T00:00:00Z","playerClass":"All","guideFormat":"Auto","guideText":"","gear":[]}
        """;
        return baseJson.Replace("\"playerClass\":\"All\"", playerClassAssignment);
    }
}

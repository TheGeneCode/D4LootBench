using System.Text.Json;
using System.Text.Json.Serialization;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
using D4LootBench.Core.Serialization;

namespace D4LootBench.Core.Profiles;

/// <summary>Maps <see cref="ProgressionProfile"/> to/from its on-disk JSON contract via explicit
/// <c>Stored*</c> DTOs, so the persisted schema survives domain-record renames. Hashes are written
/// as <c>0x…</c> strings and enums as their names. Does not depend on <c>FilterDataContext</c>.</summary>
public static class ProfileSerializer
{
    private const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new HexUInt32Converter() },
    };

    /// <summary>Serializes a profile to indented JSON stamped with <c>schemaVersion: 1</c>.</summary>
    /// <param name="profile">The domain profile to persist.</param>
    /// <returns>The JSON document text.</returns>
    public static string Serialize(ProgressionProfile profile)
        => JsonSerializer.Serialize(ToStored(profile), JsonOptions);

    /// <summary>Parses a profile from its JSON document.</summary>
    /// <param name="json">The JSON text produced by <see cref="Serialize"/>.</param>
    /// <returns>The reconstructed domain profile.</returns>
    /// <exception cref="JsonException">The input is malformed, deserializes to <c>null</c>, or
    /// contains a field value (e.g. a hash string) that fails converter-level parsing.</exception>
    public static ProgressionProfile Deserialize(string json)
    {
        try
        {
            var stored = JsonSerializer.Deserialize<StoredProfile>(json, JsonOptions)
                ?? throw new JsonException("Profile JSON is empty.");
            return FromStored(stored);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            // HexUInt32Converter parses hash strings with FormatException/OverflowException
            // (uint.Parse / Convert.ToUInt32) rather than JsonException; normalize so every
            // malformed-input path honors this method's documented JsonException contract.
            throw new JsonException($"Profile JSON contains an invalid value: {ex.Message}", ex);
        }
    }

    private static StoredProfile ToStored(ProgressionProfile profile) => new(
        CurrentSchemaVersion,
        profile.Id,
        profile.Name,
        profile.CreatedUtc,
        profile.ModifiedUtc,
        profile.PlayerClass,
        profile.GuideFormat,
        profile.GuideText,
        [.. profile.Gear.Select(ToStored)],
        profile.OverrideBlockCode,
        profile.OverriddenByBlockCode);

    private static StoredGearItem ToStored(GearItem item) => new(
        item.Slot,
        item.ItemTypeName,
        item.ItemPower,
        item.Rarity,
        item.IsAncestral,
        item.UniqueHash,
        [.. item.Affixes.Select(ToStored)]);

    private static StoredGearAffix ToStored(GearAffix affix) => new(
        affix.RawText,
        affix.ResolvedName,
        affix.AffixHash,
        affix.IsGreaterAffix);

    private static ProgressionProfile FromStored(StoredProfile stored) => new()
    {
        Id = stored.Id,
        Name = stored.Name ?? "",
        CreatedUtc = stored.CreatedUtc,
        ModifiedUtc = stored.ModifiedUtc,
        PlayerClass = stored.PlayerClass,
        GuideFormat = stored.GuideFormat,
        GuideText = stored.GuideText ?? "",
        Gear = [.. (stored.Gear ?? []).Select(FromStored)],
        OverrideBlockCode = stored.OverrideBlockCode,
        OverriddenByBlockCode = stored.OverriddenByBlockCode,
    };

    private static GearItem FromStored(StoredGearItem stored) => new()
    {
        Slot = stored.Slot,
        ItemTypeName = stored.ItemTypeName,
        ItemPower = stored.ItemPower,
        Rarity = stored.Rarity,
        IsAncestral = stored.IsAncestral,
        UniqueHash = stored.UniqueHash,
        Affixes = [.. (stored.Affixes ?? []).Select(FromStored)],
    };

    private static GearAffix FromStored(StoredGearAffix stored) => new()
    {
        RawText = stored.RawText ?? "",
        ResolvedName = stored.ResolvedName,
        AffixHash = stored.AffixHash,
        IsGreaterAffix = stored.IsGreaterAffix,
    };

    private sealed record StoredProfile(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("createdUtc")] DateTimeOffset CreatedUtc,
        [property: JsonPropertyName("modifiedUtc")] DateTimeOffset ModifiedUtc,
        [property: JsonPropertyName("playerClass")] PlayerClass PlayerClass,
        [property: JsonPropertyName("guideFormat")] BuildGuideFormat GuideFormat,
        [property: JsonPropertyName("guideText")] string GuideText,
        [property: JsonPropertyName("gear")] List<StoredGearItem> Gear,
        [property: JsonPropertyName("overrideBlockCode")] string? OverrideBlockCode = null,
        [property: JsonPropertyName("overriddenByBlockCode")] string? OverriddenByBlockCode = null);

    private sealed record StoredGearItem(
        [property: JsonPropertyName("slot")] GearSlot Slot,
        [property: JsonPropertyName("itemTypeName")] string? ItemTypeName,
        [property: JsonPropertyName("itemPower")] int? ItemPower,
        [property: JsonPropertyName("rarity")] ItemRarity Rarity,
        [property: JsonPropertyName("isAncestral")] bool IsAncestral,
        [property: JsonPropertyName("uniqueHash")] uint? UniqueHash,
        [property: JsonPropertyName("affixes")] List<StoredGearAffix> Affixes);

    private sealed record StoredGearAffix(
        [property: JsonPropertyName("rawText")] string RawText,
        [property: JsonPropertyName("resolvedName")] string? ResolvedName,
        [property: JsonPropertyName("affixHash")] uint? AffixHash,
        [property: JsonPropertyName("isGreaterAffix")] bool IsGreaterAffix);
}

using System.Text.Json;
using System.Text.Json.Serialization;
using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.Core.Serialization;

/// <summary>
/// Serializes a <see cref="GreaterAffixEntry"/> as <c>{ affixId, affixName, affixIdEcho }</c>.
/// The name is informational only; <c>affixId</c> is authoritative on deserialize.
/// </summary>
public sealed class GreaterAffixEntryConverter : JsonConverter<GreaterAffixEntry>
{
    public override GreaterAffixEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected object.");

        uint affixId = 0;
        uint? affixIdEcho = null;
        string? nameFallback = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();
            var prop = reader.GetString();
            reader.Read();
            switch (prop)
            {
                case "affixId":
                case "AffixId":
                    affixId = ReadUint(ref reader);
                    break;
                case "affixIdEcho":
                case "AffixIdEcho":
                    affixIdEcho = ReadUint(ref reader);
                    break;
                case "affixName":
                case "AffixName":
                    nameFallback = reader.GetString();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (affixId == 0 && !string.IsNullOrEmpty(nameFallback)
            && FilterDataContext.Current.Affixes.TryGetByName(nameFallback, out var entry))
        {
            affixId = entry.Hash;
        }

        return new GreaterAffixEntry(affixId, affixIdEcho ?? affixId);
    }

    public override void Write(Utf8JsonWriter writer, GreaterAffixEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("affixId", FormatHex(value.AffixId));
        writer.WriteString("affixName", FilterDataContext.Current.Affixes.GetDisplayName(value.AffixId));
        writer.WriteString("affixIdEcho", FormatHex(value.AffixIdEcho));
        writer.WriteEndObject();
    }

    private static uint ReadUint(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.String
            ? ParseHex(reader.GetString()!)
            : reader.GetUInt32();

    private static uint ParseHex(string s) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(s[2..], 16)
            : uint.Parse(s);

    private static string FormatHex(uint id) => $"0x{id:x8}";
}

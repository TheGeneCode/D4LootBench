using System.Text.Json;
using System.Text.Json.Serialization;
using D4LootBench.Core.Models;

namespace D4LootBench.Core.Serialization;

/// <summary>
/// Serializes a <see cref="TalismanSetEntry"/> as <c>{ setId, setName, itemId, itemName }</c>.
/// Names are informational; the hash IDs are authoritative on deserialize.
/// </summary>
public sealed class TalismanSetEntryConverter : JsonConverter<TalismanSetEntry>
{
    public override TalismanSetEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected object.");

        uint setId = 0, itemId = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();
            var prop = reader.GetString();
            reader.Read();
            switch (prop)
            {
                case "setId":
                case "SetId":
                    setId = ReadUint(ref reader);
                    break;
                case "itemId":
                case "ItemId":
                    itemId = ReadUint(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new TalismanSetEntry(setId, itemId);
    }

    public override void Write(Utf8JsonWriter writer, TalismanSetEntry value, JsonSerializerOptions options)
    {
        var ctx = FilterDataContext.Current;
        writer.WriteStartObject();
        writer.WriteString("setId", FormatHex(value.SetId));
        writer.WriteString("setName", ctx.TalismanSets.GetSetName(value.SetId));
        writer.WriteString("itemId", FormatHex(value.ItemId));
        writer.WriteString("itemName", ctx.TalismanSets.GetItemName(value.ItemId));
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

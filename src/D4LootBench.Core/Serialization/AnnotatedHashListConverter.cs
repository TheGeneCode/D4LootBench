using System.Text.Json;
using System.Text.Json.Serialization;

namespace D4LootBench.Core.Serialization;

/// <summary>
/// Serializes <c>IReadOnlyList&lt;uint&gt;</c> as <c>[{ "id": "0x…", "name": "…" }, …]</c>
/// where each element pairs the authoritative hash with a human-readable name resolved
/// via <see cref="FilterDataContext.Current"/>. On deserialize, <c>id</c> wins when present;
/// otherwise the name is looked up. Subclasses override the name-resolution helpers to
/// pick the right catalog (affixes, item types, etc.).
/// </summary>
public abstract class AnnotatedHashListConverter : JsonConverter<IReadOnlyList<uint>>
{
    protected abstract string GetName(uint id);
    protected abstract bool TryLookupByName(string name, out uint id);

    public override IReadOnlyList<uint> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<uint>();

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected array.");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    list.Add(ParseHex(reader.GetString()!));
                    break;
                case JsonTokenType.Number:
                    list.Add(reader.GetUInt32());
                    break;
                case JsonTokenType.StartObject:
                    list.Add(ReadAnnotatedObject(ref reader));
                    break;
                default:
                    throw new JsonException($"Unexpected token {reader.TokenType} in hash list.");
            }
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<uint> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var id in value)
        {
            writer.WriteStartObject();
            writer.WriteString("id", FormatHex(id));
            writer.WriteString("name", GetName(id));
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private uint ReadAnnotatedObject(ref Utf8JsonReader reader)
    {
        uint? id = null;
        string? name = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name.");
            var prop = reader.GetString();
            reader.Read();
            switch (prop)
            {
                case "id":
                    id = reader.TokenType == JsonTokenType.String
                        ? ParseHex(reader.GetString()!)
                        : reader.GetUInt32();
                    break;
                case "name":
                    name = reader.GetString();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (id.HasValue) return id.Value;
        if (!string.IsNullOrEmpty(name) && TryLookupByName(name, out var resolved)) return resolved;
        throw new JsonException("Annotated hash entry has neither a valid 'id' nor a resolvable 'name'.");
    }

    protected static uint ParseHex(string s) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(s[2..], 16)
            : uint.Parse(s);

    protected static string FormatHex(uint id) => $"0x{id:x8}";
}

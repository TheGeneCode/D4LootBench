using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThunderEagle.FilterForge.Core.Serialization;

/// <summary>Serializes uint as "0x00xxxxxx" hex strings for human-readable JSON output.</summary>
public sealed class HexUInt32Converter : JsonConverter<uint>
{
    public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString()!;
            return str.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToUInt32(str[2..], 16)
                : uint.Parse(str);
        }
        return reader.GetUInt32();
    }

    public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options)
        => writer.WriteStringValue($"0x{value:x8}");
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThunderEagle.FilterForge.Core.Serialization;

public static class FilterJsonOptions
{
    public static JsonSerializerOptions Default { get; } = new()
    {
        WriteIndented = true,
        Converters =
        {
            new HexUInt32Converter(),
            new JsonStringEnumConverter(),
        }
    };
}

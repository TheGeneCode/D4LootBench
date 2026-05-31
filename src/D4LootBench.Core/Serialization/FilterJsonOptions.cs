using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace D4LootBench.Core.Serialization;

public static class FilterJsonOptions
{
    public static JsonSerializerOptions Default { get; } = new()
    {
        WriteIndented = true,
        // Relaxed encoder keeps affix names like "+Armor" readable rather than +Armor.
        // These files are never embedded in HTML, so HTML-escaping buys nothing here.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters =
        {
            new HexUInt32Converter(),
            new JsonStringEnumConverter(),
        }
    };
}

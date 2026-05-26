using System.Reflection;
using System.Text.Json;

namespace D4LootBench.Core.Data;

internal static class FilterDataStore
{
    private static JsonDocument? _document;
    private static readonly object _lock = new();

    public static JsonElement Root
    {
        get
        {
            if (_document is null)
            {
                lock (_lock)
                {
                    if (_document is null)
                    {
                        var text = TryLoadExternal();
                        if (text is null)
                        {
                            text = LoadEmbedded();
                        }
                        _document = JsonDocument.Parse(text);
                    }
                }
            }
            return _document.RootElement;
        }
    }

    private static string? TryLoadExternal()
    {
        // AppContext.BaseDirectory is the process directory in both normal and single-file builds.
        // Assembly.Location returns "" in single-file bundles, so it cannot be used here.
        var path = Path.Combine(AppContext.BaseDirectory, "d4-data.json");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string LoadEmbedded()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("D4LootBench.Core.Data.d4-data.json")
            ?? throw new FileNotFoundException("Embedded resource d4-data.json not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

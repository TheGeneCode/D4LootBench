using System.Reflection;
using System.Text.Json;

namespace D4Loot.Core.Data;

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
        var dir = Path.GetDirectoryName(typeof(FilterDataStore).Assembly.Location);
        while (dir is not null)
        {
            var path = Path.Combine(dir, "d4-data.json");
            if (File.Exists(path))
                return File.ReadAllText(path);
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string LoadEmbedded()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("D4Loot.Core.Data.d4-data.json")
            ?? throw new FileNotFoundException("Embedded resource d4-data.json not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

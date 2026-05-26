using System.Reflection;

namespace D4LootBench.Core.Data;

/// <summary>Exposes the embedded d4-data.json so the host app can extract it for user customization.</summary>
public static class FilterDataExporter
{
    public static Stream OpenEmbeddedStream()
    {
        var asm = Assembly.GetExecutingAssembly();
        return asm.GetManifestResourceStream("D4LootBench.Core.Data.d4-data.json")
               ?? throw new FileNotFoundException("Embedded resource d4-data.json not found in D4LootBench.Core.");
    }
}

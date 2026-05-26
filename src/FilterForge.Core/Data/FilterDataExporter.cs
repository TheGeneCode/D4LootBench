using System.Reflection;

namespace ThunderEagle.FilterForge.Core.Data;

/// <summary>Exposes the embedded d4-data.json so the host app can extract it for user customization.</summary>
public static class FilterDataExporter
{
    public static Stream OpenEmbeddedStream()
    {
        var asm = Assembly.GetExecutingAssembly();
        return asm.GetManifestResourceStream("ThunderEagle.FilterForge.Core.Data.d4-data.json")
               ?? throw new FileNotFoundException("Embedded resource d4-data.json not found in FilterForge.Core.");
    }
}

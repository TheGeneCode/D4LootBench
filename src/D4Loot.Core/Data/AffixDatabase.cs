namespace D4Loot.Core.Data;

public static class AffixDatabase
{
    private static readonly Dictionary<string, uint> _byName;
    private static readonly Dictionary<uint, string> _byHash;

    static AffixDatabase()
    {
        _byName = new Dictionary<string, uint>();
        var arr = FilterDataStore.Root.GetProperty("affixes");
        foreach (var el in arr.EnumerateArray())
        {
            var name = el.GetProperty("displayName").GetString()!;
            var hashHex = el.GetProperty("hash").GetString()!;
            var hash = Convert.ToUInt32(hashHex[2..], 16);
            _byName[name] = hash;
        }
        _byHash = _byName.ToDictionary(kv => kv.Value, kv => kv.Key);
    }

    public static IReadOnlyDictionary<string, uint> ByName => _byName;
    public static IReadOnlyDictionary<uint, string> ByHash => _byHash;

    public static string GetDisplayName(uint hash)
        => _byHash.TryGetValue(hash, out var name) ? name : $"Unknown (0x{hash:x8})";
}

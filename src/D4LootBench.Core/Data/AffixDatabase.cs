namespace D4LootBench.Core.Data;

public sealed record AffixEntry(string Name, uint Hash, IReadOnlyList<string> Classes);

public static class AffixDatabase
{
    public static IReadOnlyList<AffixEntry> All { get; }
    public static IReadOnlyDictionary<uint, AffixEntry> ByHash { get; }
    public static IReadOnlyDictionary<string, AffixEntry> ByName { get; }

    private static readonly Dictionary<string, List<AffixEntry>> _byClass;

    static AffixDatabase()
    {
        var all = new List<AffixEntry>();
        _byClass = new Dictionary<string, List<AffixEntry>>();

        var arr = FilterDataStore.Root.GetProperty("affixes");
        foreach (var el in arr.EnumerateArray())
        {
            var name = el.GetProperty("displayName").GetString()!;
            var hashHex = el.GetProperty("hash").GetString()!;
            var hash = Convert.ToUInt32(hashHex[2..], 16);
            var classes = el.GetProperty("classes")
                            .EnumerateArray()
                            .Select(c => c.GetString()!)
                            .ToList()
                            .AsReadOnly();

            var entry = new AffixEntry(name, hash, classes);
            all.Add(entry);

            foreach (var cls in classes)
            {
                if (!_byClass.TryGetValue(cls, out var list))
                {
                    list = new List<AffixEntry>();
                    _byClass[cls] = list;
                }
                list.Add(entry);
            }
        }

        // Last-write-wins for duplicate display names (e.g. "All Damage Multiplier" carries several
        // distinct per-source hashes under one name)
        var byName = new Dictionary<string, AffixEntry>();
        var byHash = new Dictionary<uint, AffixEntry>();
        foreach (var e in all) { byName[e.Name] = e; byHash[e.Hash] = e; }

        All = all.AsReadOnly();
        ByHash = byHash;
        ByName = byName;
    }

    public static IReadOnlyList<AffixEntry> ForClass(string className)
    {
        var specific = _byClass.TryGetValue(className, out var list) ? list : [];
        var all = _byClass.TryGetValue("All", out var allList) ? allList : [];
        return [.. specific, .. all];
    }

    public static string GetDisplayName(uint hash)
        => ByHash.TryGetValue(hash, out var entry) ? entry.Name : $"Unknown (0x{hash:x8})";
}

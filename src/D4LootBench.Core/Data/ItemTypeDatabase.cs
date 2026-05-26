namespace D4LootBench.Core.Data;

public sealed record ItemTypeEntry(string Name, uint Hash, string InternalName, string Category, IReadOnlyList<string> Classes);

public static class ItemTypeDatabase
{
    public static IReadOnlyList<ItemTypeEntry> All { get; }
    public static IReadOnlyDictionary<uint, ItemTypeEntry> ByHash { get; }

    public static IReadOnlyList<ItemTypeEntry> Weapons => _byCategory["Weapons"];
    public static IReadOnlyList<ItemTypeEntry> Armor => _byCategory["Armor"];
    public static IReadOnlyList<ItemTypeEntry> Accessories => _byCategory["Accessories"];
    public static IReadOnlyList<ItemTypeEntry> Special => _byCategory["Special"];

    private static readonly Dictionary<string, List<ItemTypeEntry>> _byCategory;
    private static readonly Dictionary<string, List<ItemTypeEntry>> _byClass;

    static ItemTypeDatabase()
    {
        var all = new List<ItemTypeEntry>();
        _byCategory = new Dictionary<string, List<ItemTypeEntry>>();
        _byClass    = new Dictionary<string, List<ItemTypeEntry>>();

        var arr = FilterDataStore.Root.GetProperty("itemTypes");
        foreach (var el in arr.EnumerateArray())
        {
            var name         = el.GetProperty("displayName").GetString()!;
            var hashHex      = el.GetProperty("hash").GetString()!;
            var hash         = Convert.ToUInt32(hashHex[2..], 16);
            var internalName = el.GetProperty("internalName").GetString()!;
            var category     = el.GetProperty("category").GetString()!;
            var classes      = el.GetProperty("classes")
                                 .EnumerateArray()
                                 .Select(c => c.GetString()!)
                                 .ToList()
                                 .AsReadOnly();

            var entry = new ItemTypeEntry(name, hash, internalName, category, classes);
            all.Add(entry);

            if (!_byCategory.TryGetValue(category, out var catList))
            {
                catList = new List<ItemTypeEntry>();
                _byCategory[category] = catList;
            }
            catList.Add(entry);

            foreach (var cls in classes)
            {
                if (!_byClass.TryGetValue(cls, out var clsList))
                {
                    clsList = new List<ItemTypeEntry>();
                    _byClass[cls] = clsList;
                }
                clsList.Add(entry);
            }
        }

        All    = all.AsReadOnly();
        ByHash = all.ToDictionary(e => e.Hash);
    }

    public static IReadOnlyList<ItemTypeEntry> ForClass(string className)
    {
        var specific = _byClass.TryGetValue(className, out var list) ? list : [];
        var all = _byClass.TryGetValue("All", out var allList) ? allList : [];
        return [.. specific, .. all];
    }

    public static string GetDisplayName(uint hash)
        => ByHash.TryGetValue(hash, out var entry) ? entry.Name : $"Unknown item type (0x{hash:x8})";
}

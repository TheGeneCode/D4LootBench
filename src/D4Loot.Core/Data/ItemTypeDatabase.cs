namespace D4Loot.Core.Data;

public sealed record ItemTypeEntry(string Name, uint Hash, string InternalName);

public static class ItemTypeDatabase
{
    public static IReadOnlyList<ItemTypeEntry> All { get; }
    public static IReadOnlyDictionary<uint, ItemTypeEntry> ByHash { get; }

    public static IReadOnlyList<ItemTypeEntry> Weapons => _byCategory["Weapons"];
    public static IReadOnlyList<ItemTypeEntry> Armor => _byCategory["Armor"];
    public static IReadOnlyList<ItemTypeEntry> Accessories => _byCategory["Accessories"];
    public static IReadOnlyList<ItemTypeEntry> Special => _byCategory["Special"];

    private static readonly Dictionary<string, List<ItemTypeEntry>> _byCategory;

    static ItemTypeDatabase()
    {
        var all = new List<ItemTypeEntry>();
        _byCategory = new Dictionary<string, List<ItemTypeEntry>>();

        var arr = FilterDataStore.Root.GetProperty("itemTypes");
        foreach (var el in arr.EnumerateArray())
        {
            var name = el.GetProperty("displayName").GetString()!;
            var hashHex = el.GetProperty("hash").GetString()!;
            var hash = Convert.ToUInt32(hashHex[2..], 16);
            var internalName = el.GetProperty("internalName").GetString()!;
            var category = el.GetProperty("category").GetString()!;

            var entry = new ItemTypeEntry(name, hash, internalName);
            all.Add(entry);

            if (!_byCategory.TryGetValue(category, out var list))
            {
                list = new List<ItemTypeEntry>();
                _byCategory[category] = list;
            }
            list.Add(entry);
        }

        All = all.AsReadOnly();
        ByHash = all.ToDictionary(e => e.Hash);
    }

    public static string GetDisplayName(uint hash)
        => ByHash.TryGetValue(hash, out var entry) ? entry.Name : $"Unknown item type (0x{hash:x8})";
}

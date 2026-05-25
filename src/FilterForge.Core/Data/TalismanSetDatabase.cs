namespace ThunderEagle.FilterForge.Core.Data;

public sealed record TalismanSetItemEntry(string Name, uint Hash, string InternalName);

public sealed record TalismanSetInfo(string Name, uint Hash, string InternalName,
    IReadOnlyList<TalismanSetItemEntry> Items, IReadOnlyList<string> Classes);

public static class TalismanSetDatabase
{
    public static IReadOnlyList<TalismanSetInfo> All { get; }
    public static IReadOnlyDictionary<uint, TalismanSetInfo> ByHash { get; }

    /// <summary>Flat lookup of all item hashes across every set.</summary>
    public static IReadOnlyDictionary<uint, TalismanSetItemEntry> ItemsByHash { get; }

    /// <summary>Maps item hash → the set hash the item belongs to.</summary>
    public static IReadOnlyDictionary<uint, uint> ItemToSetHash { get; }

    private static readonly Dictionary<string, List<TalismanSetInfo>> _byClass;

    static TalismanSetDatabase()
    {
        var all = new List<TalismanSetInfo>();
        _byClass = new Dictionary<string, List<TalismanSetInfo>>();

        var arr = FilterDataStore.Root.GetProperty("talismanSets");
        foreach (var el in arr.EnumerateArray())
        {
            var name         = el.GetProperty("displayName").GetString()!;
            var internalName = el.GetProperty("internalName").GetString()!;

            // Hash field is not yet populated in d4-data.json; skip entries until data is extended
            if (!el.TryGetProperty("hash", out var hashEl))
                continue;
            var hash = Convert.ToUInt32(hashEl.GetString()![2..], 16);

            var classes = el.GetProperty("classes")
                            .EnumerateArray()
                            .Select(c => c.GetString()!)
                            .ToList()
                            .AsReadOnly();

            var items = new List<TalismanSetItemEntry>();
            foreach (var item in el.GetProperty("items").EnumerateArray())
            {
                var iName    = item.GetProperty("displayName").GetString()!;
                var iInternal = item.GetProperty("internalName").GetString()!;
                if (!item.TryGetProperty("hash", out var iHashEl))
                    continue;
                var iHash = Convert.ToUInt32(iHashEl.GetString()![2..], 16);
                items.Add(new TalismanSetItemEntry(iName, iHash, iInternal));
            }

            var entry = new TalismanSetInfo(name, hash, internalName, items.AsReadOnly(), classes);
            all.Add(entry);

            foreach (var cls in classes)
            {
                if (!_byClass.TryGetValue(cls, out var list))
                {
                    list = new List<TalismanSetInfo>();
                    _byClass[cls] = list;
                }
                list.Add(entry);
            }
        }

        All = all.AsReadOnly();
        ByHash = all.ToDictionary(e => e.Hash);
        ItemsByHash = all
            .SelectMany(s => s.Items)
            .ToDictionary(i => i.Hash);
        ItemToSetHash = all
            .SelectMany(s => s.Items.Select(i => (ItemHash: i.Hash, SetHash: s.Hash)))
            .ToDictionary(x => x.ItemHash, x => x.SetHash);
    }

    public static IReadOnlyList<TalismanSetInfo> ForClass(string className)
    {
        var specific = _byClass.TryGetValue(className, out var list) ? list : [];
        var all = _byClass.TryGetValue("All", out var allList) ? allList : [];
        return [.. specific, .. all];
    }

    public static string GetSetName(uint hash)
        => ByHash.TryGetValue(hash, out var entry) ? entry.Name : $"Unknown set (0x{hash:x8})";

    public static string GetItemName(uint hash)
        => ItemsByHash.TryGetValue(hash, out var entry) ? entry.Name : $"Unknown item (0x{hash:x8})";

    public static uint GetSetHashForItem(uint itemHash)
        => ItemToSetHash.TryGetValue(itemHash, out var setHash) ? setHash : 0;
}

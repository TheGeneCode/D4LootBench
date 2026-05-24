namespace D4Loot.Core.Data;

public sealed record UniqueItemEntry(string Name, uint SnoId, string InternalName);

public static class UniqueItemDatabase
{
    public static IReadOnlyList<UniqueItemEntry> All { get; }
    public static IReadOnlyDictionary<uint, UniqueItemEntry> BySnoId { get; }

    static UniqueItemDatabase()
    {
        var all = new List<UniqueItemEntry>();

        var arr = FilterDataStore.Root.GetProperty("uniques");
        foreach (var el in arr.EnumerateArray())
        {
            var name = el.GetProperty("displayName").GetString()!;
            var snoIdHex = el.GetProperty("snoId").GetString()!;
            var snoId = Convert.ToUInt32(snoIdHex[2..], 16);
            var internalName = el.GetProperty("internalName").GetString()!;

            all.Add(new UniqueItemEntry(name, snoId, internalName));
        }

        All = all.AsReadOnly();
        BySnoId = all.ToDictionary(e => e.SnoId);
    }

    public static string GetDisplayName(uint snoId)
        => BySnoId.TryGetValue(snoId, out var entry) ? entry.Name : $"Unknown unique (0x{snoId:x8})";
}

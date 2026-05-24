namespace D4Loot.Core.Data;

/// <param name="Name">Player-facing display name.</param>
/// <param name="SnoId">SNO ID used in the filter wire format.</param>
/// <param name="InternalName">CoreTOC internal asset name.</param>
/// <param name="IsReleased">
/// False for items whose display name is a placeholder (prefix "[PH]") or whose
/// name could not be resolved (equals the internal name). These are cut or
/// unreleased items in the game files and should be excluded from UI pickers.
/// </param>
public sealed record UniqueItemEntry(string Name, uint SnoId, string InternalName, bool IsReleased);

public static class UniqueItemDatabase
{
    public static IReadOnlyList<UniqueItemEntry> All { get; }

    /// <summary>Only entries where <see cref="UniqueItemEntry.IsReleased"/> is true.</summary>
    public static IReadOnlyList<UniqueItemEntry> Released { get; }

    public static IReadOnlyDictionary<uint, UniqueItemEntry> BySnoId { get; }

    /// <summary>Hash IDs are equivalent to SNO IDs in the filter wire format.</summary>
    public static IReadOnlyDictionary<uint, UniqueItemEntry> ByHash => BySnoId;

    static UniqueItemDatabase()
    {
        var all = new List<UniqueItemEntry>();

        var arr = FilterDataStore.Root.GetProperty("uniques");
        foreach (var el in arr.EnumerateArray())
        {
            var name         = el.GetProperty("displayName").GetString()!;
            var snoIdHex     = el.GetProperty("snoId").GetString()!;
            var snoId        = Convert.ToUInt32(snoIdHex[2..], 16);
            var internalName = el.GetProperty("internalName").GetString()!;

            var isReleased = !name.StartsWith("[PH]", StringComparison.Ordinal)
                          && !string.Equals(name, internalName, StringComparison.Ordinal);

            all.Add(new UniqueItemEntry(name, snoId, internalName, isReleased));
        }

        All      = all.AsReadOnly();
        Released = all.Where(e => e.IsReleased).ToList().AsReadOnly();
        BySnoId  = all.ToDictionary(e => e.SnoId);
    }

    public static string GetDisplayName(uint snoId)
        => BySnoId.TryGetValue(snoId, out var entry) ? entry.Name : $"Unknown unique (0x{snoId:x8})";

    public static string GetDisplayNameByHash(uint hash)
        => ByHash.TryGetValue(hash, out var entry) ? entry.Name : $"Unknown unique (0x{hash:x8})";
}

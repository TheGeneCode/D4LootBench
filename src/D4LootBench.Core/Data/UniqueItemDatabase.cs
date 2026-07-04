namespace D4LootBench.Core.Data;

/// <summary>One unique item's metadata, as extracted from CoreTOC for the filter pipeline.</summary>
/// <param name="Name">Player-facing display name.</param>
/// <param name="SnoId">SNO ID used in the filter wire format.</param>
/// <param name="InternalName">CoreTOC internal asset name.</param>
/// <param name="IsReleased">
/// True for all items present in d4-data.json (extraction pipeline gates on the
/// CASC release-state flag). False only when the safety-net detects a "[PH]"
/// placeholder prefix that slipped through, which is logged as a warning.
/// </param>
/// <param name="IsMythic">
/// True for Mythic-quality unique items (Item.Meta+0x20 == 0x04 in CASC binary).
/// </param>
/// <param name="Classes">Class names this unique can drop for; <c>["All"]</c> when not class-restricted.</param>
public sealed record UniqueItemEntry(string Name, uint SnoId, string InternalName, bool IsReleased,
    bool IsMythic, IReadOnlyList<string> Classes);

public static class UniqueItemDatabase
{
    public static IReadOnlyList<UniqueItemEntry> All { get; }

    /// <summary>Gets only entries where <see cref="UniqueItemEntry.IsReleased"/> is true.</summary>
    public static IReadOnlyList<UniqueItemEntry> Released { get; }

    public static IReadOnlyDictionary<uint, UniqueItemEntry> BySnoId { get; }

    /// <summary>Gets hash IDs are equivalent to SNO IDs in the filter wire format.</summary>
    public static IReadOnlyDictionary<uint, UniqueItemEntry> ByHash => BySnoId;

    private static readonly Dictionary<string, List<UniqueItemEntry>> _byClass;

    /// <summary>Maps class identifiers in internal names to full class names.</summary>
    private static readonly Dictionary<string, string> ClassNameSegment = new()
    {
        ["Barb"] = "Barbarian",
        ["Barbarian"] = "Barbarian",
        ["Druid"] = "Druid",
        ["Necro"] = "Necromancer",
        ["Necromancer"] = "Necromancer",
        ["Rogue"] = "Rogue",
        ["Sorc"] = "Sorcerer",
        ["Sorcerer"] = "Sorcerer",
        ["Spiritborn"] = "Spiritborn",
        ["Paladin"] = "Paladin",
        ["Warlock"] = "Warlock",
    };

    /// <summary>Maps internal name prefixes to item type keys for class derivation.</summary>
    private static readonly Dictionary<string, string> PrefixToItemType = new()
    {
        ["1HSword"] = "Sword",
        ["2HSword"] = "Sword2H",
        ["1HAxe"] = "Axe",
        ["2HAxe"] = "Axe2H",
        ["1HMace"] = "Mace",
        ["2HMace"] = "Mace2H",
        ["1HDagger"] = "Dagger",
        ["Dagger"] = "Dagger",
        ["Staff"] = "Staff",
        ["2HStaff"] = "Staff",
        ["Wand"] = "Wand",
        ["1HWand"] = "Wand",
        ["Focus"] = "Focus",
        ["1HFocus"] = "Focus",
        ["Bow"] = "Bow",
        ["2HBow"] = "Bow",
        ["Crossbow"] = "Crossbow",
        ["2HCrossbow"] = "Crossbow2H",
        ["Polearm"] = "Polearm",
        ["2HPolearm"] = "Polearm",
        ["Scythe"] = "Scythe",
        ["1HScythe"] = "Scythe",
        ["2HScythe"] = "Scythe2H",
        ["Totem"] = "OffHandTotem",
        ["1HTotem"] = "OffHandTotem",
        ["OffHandTotem"] = "OffHandTotem",
        ["Shield"] = "Shield",
        ["1HShield"] = "Shield",
        ["Helm"] = "Helm",
        ["Chest"] = "ChestArmor",
        ["ChestArmor"] = "ChestArmor",
        ["Legs"] = "Legs",
        ["Pants"] = "Legs",
        ["Boots"] = "Boots",
        ["Gloves"] = "Gloves",
        ["Glove"] = "Gloves",
        ["Glaive"] = "Glaive",
        ["2HGlaive"] = "2HGlaive",
        ["Quarterstaff"] = "Quarterstaff",
        ["1HFlail"] = "1HFlail",
        ["Ring"] = "Ring",
        ["Amulet"] = "Amulet",
        ["Charm"] = "Charm",
        ["HoradricSeal"] = "HoradricSeal",
    };

    /// <summary>Hardcoded class lists for item types not in d4-data.json.</summary>
    private static readonly Dictionary<string, string[]> HardcodedTypeClasses = new()
    {
        ["Glaive"] = ["Spiritborn"],
        ["2HGlaive"] = ["Spiritborn"],
        ["Quarterstaff"] = ["Spiritborn"],
        ["1HFlail"] = ["Barbarian", "Druid", "Necromancer", "Paladin", "Warlock"],
        ["2HFlail"] = ["Barbarian", "Druid", "Necromancer", "Paladin", "Warlock"],
    };

    // Matches "x1", "X1", "X2", "QST", and any "S" followed by digits (S05, S13, S99, …)
    private static bool IsSeasonOrExpansionPrefix(string prefix) =>
        prefix is "x1" or "X1" or "X2" or "QST"
        || (prefix.Length >= 2 && prefix[0] == 'S' && prefix[1..].All(char.IsAsciiDigit));

    private static IReadOnlyList<string>? TryGetClassName(string segment) =>
        ClassNameSegment.TryGetValue(segment, out var name) ? [name] : null;

    private static IReadOnlyList<string> DeriveClasses(string internalName)
    {
        var segments = internalName.Split('_');

        // If a segment contains a specific class identifier, that's the only class
        foreach (var seg in segments)
        {
            var singleClass = TryGetClassName(seg);
            if (singleClass is not null)
                return singleClass;
        }

        // If the name has "Generic", derive from item type
        // Try first segment as item type prefix
        var prefix = segments[0];
        if (PrefixToItemType.TryGetValue(prefix, out var typeKey))
            return LookupItemTypeClasses(typeKey) ?? ["All"];

        // For items with season/expansion/quest prefixes, scan for a known type segment
        if (IsSeasonOrExpansionPrefix(prefix))
        {
            foreach (var seg in segments)
            {
                if (PrefixToItemType.TryGetValue(seg, out typeKey))
                    return LookupItemTypeClasses(typeKey) ?? ["All"];
            }
        }

        return ["All"];
    }

    private static IReadOnlyList<string>? LookupItemTypeClasses(string typeKey)
    {
        var itemType = FilterDataStore.Root.GetProperty("itemTypes")
            .EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("internalName").GetString() == typeKey);
        if (itemType.ValueKind != System.Text.Json.JsonValueKind.Undefined)
        {
            return itemType.GetProperty("classes")
                .EnumerateArray()
                .Select(c => c.GetString()!)
                .ToList()
                .AsReadOnly();
        }

        if (HardcodedTypeClasses.TryGetValue(typeKey, out var hardcoded))
            return hardcoded;

        return null;
    }

    static UniqueItemDatabase()
    {
        var all = new List<UniqueItemEntry>();
        _byClass = new Dictionary<string, List<UniqueItemEntry>>();

        var arr = FilterDataStore.Root.GetProperty("uniques");
        foreach (var el in arr.EnumerateArray())
        {
            var name = el.GetProperty("displayName").GetString()!;
            var snoIdHex = el.GetProperty("snoId").GetString()!;
            var snoId = Convert.ToUInt32(snoIdHex[2..], 16);
            var internalName = el.GetProperty("internalName").GetString()!;
            var isMythic = el.GetProperty("isMythic").GetBoolean();

            // Extraction pipeline gates on CASC release-state; anything in the JSON is released.
            // [PH] prefix is a safety-net: log and exclude if one slips through.
            var isReleased = true;
            if (name.StartsWith("[PH]", StringComparison.Ordinal))
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"[UniqueItemDatabase] Placeholder item in d4-data.json: {internalName} (\"{name}\")");
                isReleased = false;
            }

            var classes = DeriveClasses(internalName);
            var entry = new UniqueItemEntry(name, snoId, internalName, isReleased, isMythic, classes);
            all.Add(entry);

            foreach (var cls in classes)
            {
                if (!_byClass.TryGetValue(cls, out var list))
                {
                    list = new List<UniqueItemEntry>();
                    _byClass[cls] = list;
                }
                list.Add(entry);
            }
        }

        All = all.AsReadOnly();
        Released = all.Where(e => e.IsReleased).ToList().AsReadOnly();
        BySnoId = all.ToDictionary(e => e.SnoId);
    }

    public static IReadOnlyList<UniqueItemEntry> ForClass(string className)
    {
        var specific = _byClass.TryGetValue(className, out var list) ? list : [];
        var all = _byClass.TryGetValue("All", out var allList) ? allList : [];
        return [.. specific, .. all];
    }

    public static string GetDisplayName(uint snoId)
        => BySnoId.TryGetValue(snoId, out var entry) ? entry.Name : $"Unknown unique (0x{snoId:x8})";

    public static string GetDisplayNameByHash(uint hash)
        => ByHash.TryGetValue(hash, out var entry) ? entry.Name : $"Unknown unique (0x{hash:x8})";
}

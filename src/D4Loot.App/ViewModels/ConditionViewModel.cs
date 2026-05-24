using D4Loot.Core.Data;
using D4Loot.Core.Models;

namespace D4Loot.App.ViewModels;

public sealed class ConditionViewModel
{
    public Condition Model { get; }

    public string TypeName => Model switch
    {
        ItemPowerCondition      => "Item Power",
        RarityCondition         => "Rarity",
        ItemPropertiesCondition => "Item Properties",
        GreaterAffixCondition   => "Greater Affix",
        CodexCondition          => "Codex of Power",
        ItemTypeCondition       => "Item Type",
        AffixCondition          => "Required Affixes",
        OptionalAffixCondition  => "Optional Affixes",
        UnknownCondition u      => $"Unknown ({u.ConditionType})",
        _                       => "Unknown"
    };

    public string Summary => Model switch
    {
        ItemPowerCondition ip      => ip.Maximum == 0 ? $"{ip.Minimum}+" : $"{ip.Minimum} – {ip.Maximum}",
        RarityCondition r          => FormatRarityFlags(r.Mask),
        ItemPropertiesCondition ip => ip.PropertyMask == 4 ? "Ancestral" : $"Mask = {ip.PropertyMask}",
        GreaterAffixCondition ga   => $"Min {ga.MinimumCount}",
        CodexCondition             => "",
        ItemTypeCondition it       => FormatList(it.TypeIds, "type"),
        AffixCondition a           => FormatList(a.AffixIds, "affix", a.MinimumCount),
        OptionalAffixCondition oa  => FormatList(oa.AffixIds, "affix"),
        UnknownCondition u         => $"{u.RawBytes.Length} raw byte(s)",
        _                          => ""
    };

    public string FullList => Model switch
    {
        ItemTypeCondition it       => string.Join(", ", it.TypeIds.Select(LookupName)),
        AffixCondition a           => string.Join(", ", a.AffixIds.Select(LookupName)),
        OptionalAffixCondition oa  => string.Join(", ", oa.AffixIds.Select(LookupName)),
        _                          => Summary
    };

    public ConditionViewModel(Condition model) => Model = model;

    private static string FormatList(IReadOnlyList<uint> ids, string label, int? count = null)
    {
        if (ids.Count == 0)
            return $"0 {label}(s)";

        var names = ids.Select(LookupName).ToList();
        var prefix = count.HasValue
            ? $"{ids.Count} {label}(es), min {count}: "
            : $"{ids.Count} {label}(s): ";

        var preview = names.Count <= 3
            ? string.Join(", ", names)
            : $"{string.Join(", ", names.Take(3))}, …";

        return prefix + preview;
    }

    private static string LookupName(uint id)
    {
        if (AffixDatabase.ByHash.TryGetValue(id, out var affixName))
            return affixName;
        if (SkillDatabase.ByHash.TryGetValue(id, out var skillEntry))
            return skillEntry.Name;
        if (ItemTypeDatabase.ByHash.TryGetValue(id, out var itemTypeEntry))
            return itemTypeEntry.Name;
        return $"0x{id:x8}";
    }

    private static string FormatRarityFlags(RarityFlags flags)
    {
        if (flags == RarityFlags.All) return "All";
        var parts = new List<string>(7);
        if (flags.HasFlag(RarityFlags.Common))    parts.Add("Common");
        if (flags.HasFlag(RarityFlags.Magic))     parts.Add("Magic");
        if (flags.HasFlag(RarityFlags.Rare))      parts.Add("Rare");
        if (flags.HasFlag(RarityFlags.Legendary)) parts.Add("Legendary");
        if (flags.HasFlag(RarityFlags.Unique))    parts.Add("Unique");
        if (flags.HasFlag(RarityFlags.Mythic))    parts.Add("Mythic");
        if (flags.HasFlag(RarityFlags.Talisman))  parts.Add("Talisman");
        return parts.Count == 0 ? "None" : string.Join(", ", parts);
    }
}

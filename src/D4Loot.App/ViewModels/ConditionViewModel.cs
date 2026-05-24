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
        SpecificUniqueCondition => "Specific Unique",
        TalismanSetCondition    => "Talisman Set",
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
        ItemTypeCondition it       => $"{it.TypeIds.Count} type{(it.TypeIds.Count == 1 ? "" : "s")}",
        AffixCondition a           => $"min {a.MinimumCount} of {a.AffixIds.Count}",
        OptionalAffixCondition oa  => oa.MinimumCount > 0 ? $"min {oa.MinimumCount} of {oa.AffixIds.Count}" : $"any of {oa.AffixIds.Count}",
        SpecificUniqueCondition su => $"{su.UniqueIds.Count} unique{(su.UniqueIds.Count == 1 ? "" : "s")}",
        TalismanSetCondition ts    => ts.SetIds.Count == 0 && ts.SetEntries.Count == 0
                                        ? "any set"
                                        : $"{ts.SetIds.Count} set{(ts.SetIds.Count == 1 ? "" : "s")}",
        UnknownCondition u         => $"{u.RawBytes.Length} raw byte(s)",
        _                          => ""
    };

    public bool HasItems => Model is ItemTypeCondition or AffixCondition or OptionalAffixCondition or SpecificUniqueCondition or TalismanSetCondition;

    public IReadOnlyList<string> Items => Model switch
    {
        ItemTypeCondition it       => it.TypeIds.Select(LookupName).ToList(),
        AffixCondition a           => a.AffixIds.Select(LookupName).ToList(),
        OptionalAffixCondition oa  => oa.AffixIds.Select(LookupName).ToList(),
        SpecificUniqueCondition su => su.UniqueIds.Select(LookupUniqueName).ToList(),
        TalismanSetCondition ts    => ts.SetIds.Select(TalismanSetDatabase.GetSetName).ToList(),
        _                          => []
    };

    public ConditionViewModel(Condition model) => Model = model;

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

    private static string LookupUniqueName(uint snoId)
    {
        if (UniqueItemDatabase.BySnoId.TryGetValue(snoId, out var entry))
            return entry.Name;
        return $"0x{snoId:x8}";
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

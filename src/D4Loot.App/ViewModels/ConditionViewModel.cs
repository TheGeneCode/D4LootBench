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
        ItemTypeCondition it       => $"{it.TypeIds.Count} type(s)",
        AffixCondition a           => $"{a.AffixIds.Count} affix(es), min {a.MinimumCount}",
        OptionalAffixCondition oa  => $"{oa.AffixIds.Count} affix(es), any",
        UnknownCondition u         => $"{u.RawBytes.Length} raw byte(s)",
        _                          => ""
    };

    public ConditionViewModel(Condition model) => Model = model;

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

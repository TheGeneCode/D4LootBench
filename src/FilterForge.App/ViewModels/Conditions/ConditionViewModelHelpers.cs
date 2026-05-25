using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels.Conditions;

internal static class ConditionViewModelHelpers
{
    internal static string FormatRarityFlags(RarityFlags flags)
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

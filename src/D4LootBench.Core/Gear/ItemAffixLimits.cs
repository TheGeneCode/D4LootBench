namespace D4LootBench.Core.Gear;

/// <summary>Rollable-affix limits used to decide when an item is "maxed" on target affixes.
/// Excludes tempered affixes (added post-drop) and the legendary aspect / unique power slot is
/// counted in the cap but is never a Greater Affix.</summary>
public static class ItemAffixLimits
{
    /// <summary>Maximum number of affixes that can be Greater Affixes on any item — only the
    /// normal rollable affixes can roll greater; aspect and tempered affixes never do.</summary>
    public const int MaxGreaterAffixCount = 3;

    /// <summary>Gets the maximum number of rollable affixes an item of <paramref name="rarity"/>
    /// can hold (Magic = 3; every other rarity = 4, where Legendary/Unique spend one slot on
    /// their inherent aspect/unique affix). Unknown falls back to the 4-affix ceiling.</summary>
    /// <param name="rarity">The item rarity.</param>
    /// <returns>The rollable-affix cap.</returns>
    public static int RollableAffixCap(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => 3,
        _ => 4,
    };
}

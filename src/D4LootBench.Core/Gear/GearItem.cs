namespace D4LootBench.Core.Gear;

/// <summary>An equipped item read from a tooltip screenshot.</summary>
public sealed record GearItem
{
    /// <summary>Gets equipment slot; <see cref="GearSlot.Unknown"/> when it could not be determined.</summary>
    public GearSlot Slot { get; init; } = GearSlot.Unknown;

    /// <summary>Gets catalog item-type display name, or <c>null</c> when unmatched.</summary>
    public string? ItemTypeName { get; init; }

    /// <summary>Gets item power, or <c>null</c> when the tooltip did not expose it.</summary>
    public int? ItemPower { get; init; }

    /// <summary>Gets inferred rarity tier.</summary>
    public ItemRarity Rarity { get; init; } = ItemRarity.Unknown;

    /// <summary>Gets a value indicating whether the tooltip carries the "Ancestral" marker.</summary>
    public bool IsAncestral { get; init; }

    /// <summary>Gets parsed affix lines in reading order.</summary>
    public IReadOnlyList<GearAffix> Affixes { get; init; } = [];
}

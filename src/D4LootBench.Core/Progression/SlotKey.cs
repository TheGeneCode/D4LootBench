namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Gear;

/// <summary>Identity of one physical equipment slot: a <see cref="GearSlot"/> plus a 0-based ordinal
/// distinguishing duplicates (Ring 0/1, dual weapons 0/1) and an optional <paramref name="ItemType"/>
/// discriminator. Weapon/Offhand slots carry their catalog item-type name (e.g. "Two-Handed Sword") so
/// each distinct weapon type is its own slot; other slots leave it <c>null</c> and use <paramref name="Ordinal"/>.
/// Single-instance slots use ordinal 0.</summary>
public readonly record struct SlotKey(GearSlot Slot, int Ordinal = 0, string? ItemType = null)
{
    /// <summary>Convenience factory.</summary>
    /// <param name="slot">The gear slot.</param>
    /// <param name="ordinal">The 0-based ordinal distinguishing duplicate slots.</param>
    /// <param name="itemType">The optional item-type discriminator (Weapon/Offhand only).</param>
    /// <returns>A new <see cref="SlotKey"/>.</returns>
    public static SlotKey For(GearSlot slot, int ordinal = 0, string? itemType = null) => new(slot, ordinal, itemType);

    /// <inheritdoc/>
    public override string ToString() =>
        ItemType is not null ? $"{Slot} ({ItemType})"
        : Ordinal == 0 ? Slot.ToString()
        : $"{Slot}#{Ordinal + 1}";
}

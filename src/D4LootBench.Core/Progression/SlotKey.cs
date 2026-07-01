namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Gear;

/// <summary>Identity of one physical equipment slot: a <see cref="GearSlot"/> plus a 0-based ordinal
/// distinguishing duplicates (Ring 0/1, dual weapons 0/1). Single-instance slots use ordinal 0.</summary>
public readonly record struct SlotKey(GearSlot Slot, int Ordinal = 0)
{
    /// <summary>Convenience factory.</summary>
    /// <param name="slot">The gear slot.</param>
    /// <param name="ordinal">The 0-based ordinal distinguishing duplicate slots.</param>
    /// <returns>A new <see cref="SlotKey"/>.</returns>
    public static SlotKey For(GearSlot slot, int ordinal = 0) => new(slot, ordinal);

    /// <inheritdoc/>
    public override string ToString() => Ordinal == 0 ? Slot.ToString() : $"{Slot}#{Ordinal + 1}";
}

namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Gear;

/// <summary>Identity of one physical equipment slot: a <see cref="GearSlot"/> plus a 0-based ordinal
/// distinguishing duplicates (Ring 0/1, dual weapons 0/1) and an optional class-aware
/// <paramref name="Role"/>. Weapon/Offhand slots carry the <see cref="WeaponSlotRole"/> they play for
/// the class (e.g. Slicing, Mainhand, TwoHand) so a role — which admits a whole SET of item types — is
/// the slot's discriminator; other slots leave it <see cref="WeaponSlotRole.None"/> and use
/// <paramref name="Ordinal"/>. Single-instance slots use ordinal 0.</summary>
public readonly record struct SlotKey(GearSlot Slot, int Ordinal = 0, WeaponSlotRole Role = WeaponSlotRole.None)
{
    /// <summary>Convenience factory.</summary>
    /// <param name="slot">The gear slot.</param>
    /// <param name="ordinal">The 0-based ordinal distinguishing duplicate slots.</param>
    /// <param name="role">The optional weapon slot role (Weapon/Offhand only).</param>
    /// <returns>A new <see cref="SlotKey"/>.</returns>
    public static SlotKey For(GearSlot slot, int ordinal = 0, WeaponSlotRole role = WeaponSlotRole.None) => new(slot, ordinal, role);

    /// <inheritdoc/>
    public override string ToString() =>
        Role != WeaponSlotRole.None ? Role.ToString()
        : Ordinal == 0 ? Slot.ToString()
        : $"{Slot}#{Ordinal + 1}";
}

namespace D4LootBench.Core.Progression;

/// <summary>The role a weapon/offhand slot plays for a class. A role admits a SET of item
/// types (resolved via <see cref="WeaponRoleMap"/>). <see cref="None"/> = a non-weapon slot,
/// keyed by <c>GearSlot</c>+ordinal only.</summary>
public enum WeaponSlotRole
{
    /// <summary>A non-weapon slot, keyed by <c>GearSlot</c>+ordinal only.</summary>
    None = 0,

    /// <summary>Any class one-handed weapon.</summary>
    Mainhand,

    /// <summary>Barbarian: a second 1H weapon; others: Focus/Totem/Shield (or 1H dual-wield).</summary>
    Offhand,

    /// <summary>A single two-handed weapon spanning both hands (non-Barbarian).</summary>
    TwoHand,

    /// <summary>Barbarian: Two-Handed Mace.</summary>
    Bludgeoning,

    /// <summary>Barbarian: Polearm / Two-Handed Sword / Two-Handed Axe.</summary>
    Slicing,
}

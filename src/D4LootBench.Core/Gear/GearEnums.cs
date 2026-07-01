namespace D4LootBench.Core.Gear;

/// <summary>Equipment slot a parsed tooltip maps to.</summary>
public enum GearSlot
{
    Unknown,
    Helm,
    ChestArmor,
    Gloves,
    Pants,
    Boots,
    Amulet,
    Ring,
    Weapon,
    Offhand,
}

/// <summary>Item rarity tier inferred from tooltip text.</summary>
public enum ItemRarity
{
    Unknown,
    Common,
    Magic,
    Rare,
    Legendary,
    Unique,
    Mythic,
}

/// <summary>Structural confidence in a parse (no engine score is available from Windows OCR).</summary>
public enum GearParseConfidence
{
    High,
    Low,
}

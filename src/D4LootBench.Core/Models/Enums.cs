namespace D4LootBench.Core.Models;

public enum Visibility
{
    Show    = 0,
    Recolor = 2,
    HideAll = 3
}

[Flags]
public enum RarityFlags : uint
{
    None         = 0x00,
    Common       = 0x01,
    Magic        = 0x02,
    Rare         = 0x04,
    Legendary    = 0x08,
    Unique       = 0x10,
    Mythic       = 0x20,
    Talisman     = 0x40,
    All          = 0x7F,
    LegendaryPlus = Legendary | Unique | Mythic | Talisman
}

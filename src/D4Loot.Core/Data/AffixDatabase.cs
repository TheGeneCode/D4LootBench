namespace D4Loot.Core.Data;

/// <summary>
/// Affix name ↔ hash mappings confirmed via single-affix filter exports, Season 13.
/// Source: https://github.com/Upsilon72/d4-filter-generator
/// </summary>
public static class AffixDatabase
{
    private static readonly Dictionary<string, uint> _byName = new()
    {
        // Offensive — damage
        ["Weapon Damage"]                      = 0x0027fc93,
        ["All Damage"]                         = 0x001bead4,
        ["Attack Speed"]                       = 0x001beab4,
        ["Critical Strike Chance"]             = 0x001beace,
        ["Critical Strike Damage"]             = 0x001bead2,
        ["Vulnerable Damage Multiplier"]       = 0x001bfc80,
        ["Core Skills"]                        = 0x001d6e31,
        ["All Skills"]                         = 0x00273c0a,
        ["Damage Over Time Multiplier"]        = 0x001bead6,
        ["Cold Damage Multiplier"]             = 0x00270af5,
        ["Fire Damage Multiplier"]             = 0x00270af7,
        ["Holy Damage Multiplier"]             = 0x00270aff,
        ["Lightning Damage Multiplier"]        = 0x00270afd,
        ["Physical Damage Multiplier"]         = 0x00270ad0,
        ["Poison Damage Multiplier"]           = 0x00270afb,
        ["Shadow Damage Multiplier"]           = 0x00270af9,

        // Offensive — primary stats
        ["Strength"]                           = 0x001beac2,
        ["Intelligence"]                       = 0x001beabe,
        ["Willpower"]                          = 0x001beac6,
        ["Dexterity"]                          = 0x001beaba,
        ["Thorns"]                             = 0x001beb22,
        ["% Cooldown Reduction"]               = 0x001beab8,

        // Defensive
        ["Maximum Life"]                       = 0x001bead8,
        ["Life Regeneration"]                  = 0x001beada,
        ["Life On Hit"]                        = 0x001d5e13,
        ["Life on Kill"]                       = 0x0025da8c,
        ["Armor"]                              = 0x001beab2,
        ["Resistance to All Elements"]         = 0x001bfd38,
        ["Fire Resistance"]                    = 0x001beaee,
        ["Cold Resistance"]                    = 0x001beb2e,
        ["Lightning Resistance"]               = 0x001beaf2,
        ["Poison Resistance"]                  = 0x001beaf4,
        ["Shadow Resistance"]                  = 0x001beaf6,
        ["Physical Resistance"]                = 0x002557e4,
        ["Damage Reduction"]                   = 0x001d6e63,
        ["Dodge Chance"]                       = 0x001bfc85,

        // Resource
        ["Maximum Resource"]                   = 0x001bfc79,
        ["Energy Regeneration"]                = 0x001d5e30,
        ["Essence Regeneration"]               = 0x001d5e3a,
        ["Fury Regeneration"]                  = 0x001d5e38,
        ["Mana Regeneration"]                  = 0x001d5e36,
        ["Spirit Regeneration"]                = 0x001d5e33,
        ["Vigor Regeneration"]                 = 0x001eb549,
        ["Faith Regeneration"]                 = 0x002674b9,
        ["Wrath Regeneration"]                 = 0x0026a37c,
        ["Energy On Kill"]                     = 0x001d5e25,
        ["Essence On Kill"]                    = 0x001d5e27,
        ["Fury On Kill"]                       = 0x001d5e29,
        ["Mana On Kill"]                       = 0x001d5e2b,
        ["Spirit On Kill"]                     = 0x001d5e2d,
        ["Vigor On Kill"]                      = 0x001eb481,
        ["Faith On Kill"]                      = 0x002674bb,
        ["Wrath every 10 Kills"]               = 0x0026a374,
        ["Resource Cost Reduction"]            = 0x001d3a0f,
        ["Resource Generation"]                = 0x001beb20,
        ["Lucky Hit Restore Primary Resource"] = 0x0024527f,

        // Utility
        ["Potion Capacity"]                    = 0x001beae2,
        ["Lucky Hit Chance"]                   = 0x001beadc,
        ["Healing Received"]                   = 0x001bfcbf,
        ["Fortify Generation"]                 = 0x00266b1e,
        ["Barrier Generation"]                 = 0x00266b22,

        // Mobility
        ["Movement Speed"]                     = 0x001beade,
        ["Attacks Reduce Evade Cooldown"]      = 0x0026c56c,
        ["Maximum Evade Charge"]               = 0x0026c56e,
        ["Evade Grants Movement Speed"]        = 0x0026c570
    };

    private static readonly Dictionary<uint, string> _byHash =
        _byName.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static IReadOnlyDictionary<string, uint> ByName => _byName;
    public static IReadOnlyDictionary<uint, string> ByHash => _byHash;

    public static string GetDisplayName(uint hash)
        => _byHash.TryGetValue(hash, out var name) ? name : $"Unknown (0x{hash:x8})";
}

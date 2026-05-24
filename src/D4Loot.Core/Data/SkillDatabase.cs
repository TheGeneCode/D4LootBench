namespace D4Loot.Core.Data;

public sealed record SkillEntry(string Name, uint Hash, string ClassName, bool InGameVerified);

/// <summary>
/// Skill rank hash IDs. Sources:
///   - InGameVerified=true: confirmed via single-skill filter exports (Upsilon72, Season 13).
///   - InGameVerified=false: datamined from DiabloTools/d4data CoreTOC_flat.json (build 3.0.2.71886).
/// NOTE: Upsilon72's original skill labels were incorrect for most Warlock entries; this database
/// uses names derived directly from CoreTOC internal names.
/// </summary>
public static class SkillDatabase
{
    // ── Generic ──────────────────────────────────────────────────────────────

    public static IReadOnlyList<SkillEntry> Generic { get; } =
    [
        new("All Skills",        0x00273C0A, "All",     InGameVerified: false),
        new("Core Skills",       0x001D6E31, "All",     InGameVerified: false),
        new("Basic Skills",      0x001D6E2F, "All",     InGameVerified: false),
        new("Defensive Skills",  0x001D6E2B, "All",     InGameVerified: false)
    ];

    // ── Barbarian ─────────────────────────────────────────────────────────────

    public static IReadOnlyList<SkillEntry> Barbarian { get; } =
    [
        // Basic
        new("Bash",              0x001C60BC, "Barbarian", InGameVerified: false),
        new("Flay",              0x001C60C0, "Barbarian", InGameVerified: false),
        new("Frenzy",            0x001C60C2, "Barbarian", InGameVerified: false),
        new("Lunging Strike",    0x001C60C4, "Barbarian", InGameVerified: false),
        // Core
        new("Hammer of the Ancients", 0x001C68CB, "Barbarian", InGameVerified: false),
        new("Rend",              0x001C68F6, "Barbarian", InGameVerified: false),
        new("Double Swing",      0x001C6908, "Barbarian", InGameVerified: false),
        new("Upheaval",          0x001C690E, "Barbarian", InGameVerified: false),
        new("Whirlwind",         0x001C6920, "Barbarian", InGameVerified: false),
        // Defensive/Special
        new("Challenging Shout", 0x001C692A, "Barbarian", InGameVerified: false),
        new("Charge",            0x001C692C, "Barbarian", InGameVerified: false),
        new("Death Blow",        0x001C692E, "Barbarian", InGameVerified: false),
        new("Ground Stomp",      0x001C6935, "Barbarian", InGameVerified: false),
        new("Iron Skin",         0x001C6938, "Barbarian", InGameVerified: false),
        new("Kick",              0x001C693A, "Barbarian", InGameVerified: false),
        new("Leap",              0x001C6941, "Barbarian", InGameVerified: false),
        new("Rallying Cry",      0x001C6943, "Barbarian", InGameVerified: false),
        new("Rupture",           0x001C6945, "Barbarian", InGameVerified: false),
        new("Steel Grasp",       0x001C6947, "Barbarian", InGameVerified: false),
        new("War Cry",           0x001C6949, "Barbarian", InGameVerified: false),
        // Ultimate (Season 7+ additions)
        new("Call of the Ancients", 0x002782A5, "Barbarian", InGameVerified: false),
        new("Dust Devils",       0x002782A9, "Barbarian", InGameVerified: false),
        new("Earthquake",        0x002782AB, "Barbarian", InGameVerified: false),
        new("Iron Shrapnel",     0x002782AF, "Barbarian", InGameVerified: false),
        // Categories
        new("Brawling Skills",      0x001D6E25, "Barbarian", InGameVerified: false),
        new("Weapon Mastery Skills", 0x001D6E27, "Barbarian", InGameVerified: false),
        new("Bludgeoning Skills",   0x00280B83, "Barbarian", InGameVerified: false),
        new("Dual Wield Skills",    0x00280B85, "Barbarian", InGameVerified: false),
        new("Slashing Skills",      0x00280B87, "Barbarian", InGameVerified: false)
    ];

    // ── Druid ─────────────────────────────────────────────────────────────────

    public static IReadOnlyList<SkillEntry> Druid { get; } =
    [
        // Basic
        new("Earthspike",        0x001CC052, "Druid", InGameVerified: false),
        new("Claw",              0x001CC054, "Druid", InGameVerified: false),
        new("Storm Strike",      0x001CC056, "Druid", InGameVerified: false),
        new("Wind Shear",        0x001CC058, "Druid", InGameVerified: false),
        new("Maul",              0x001CC060, "Druid", InGameVerified: false),
        // Core
        new("Landslide",         0x001CC062, "Druid", InGameVerified: false),
        new("Pulverize",         0x001CC064, "Druid", InGameVerified: false),
        new("Tornado",           0x001CC066, "Druid", InGameVerified: false),
        new("Lightning Storm",   0x001CC068, "Druid", InGameVerified: false),
        new("Shred",             0x001CC06A, "Druid", InGameVerified: false),
        // Defensive/Special
        new("Blood Howl",        0x001CC06D, "Druid", InGameVerified: false),
        new("Boulder",           0x001CC06F, "Druid", InGameVerified: false),
        new("Cyclone Armor",     0x001CC071, "Druid", InGameVerified: false),
        new("Debilitating Roar", 0x001CC073, "Druid", InGameVerified: false),
        new("Earthen Bulwark",   0x001CC075, "Druid", InGameVerified: false),
        new("Hurricane",         0x001CC077, "Druid", InGameVerified: false),
        new("Rabies",            0x001CC183, "Druid", InGameVerified: false),
        new("Ravens",            0x001CC185, "Druid", InGameVerified: false),
        new("Trample",           0x001CC188, "Druid", InGameVerified: false),
        new("Vine Creeper",      0x001CC18A, "Druid", InGameVerified: false),
        new("Wolves",            0x001CC18D, "Druid", InGameVerified: false),
        // Ultimate (Season 7+ additions)
        new("Human",             0x002782B7, "Druid", InGameVerified: false),
        new("Versatile",         0x002782B9, "Druid", InGameVerified: false),
        // Categories
        new("Companion Skills",      0x001D6E2D, "Druid", InGameVerified: false),
        new("Wrath Skills",          0x001D6E33, "Druid", InGameVerified: false),
        new("Earth Skills",          0x00280B89, "Druid", InGameVerified: false),
        new("Nature Magic Skills",   0x00280B8B, "Druid", InGameVerified: false),
        new("Shapeshifting Skills",  0x00280B8D, "Druid", InGameVerified: false),
        new("Storm Skills",          0x00280B8F, "Druid", InGameVerified: false),
        new("Werebear Skills",       0x00280B91, "Druid", InGameVerified: false),
        new("Werewolf Skills",       0x00280B93, "Druid", InGameVerified: false)
    ];

    // ── Necromancer ───────────────────────────────────────────────────────────

    public static IReadOnlyList<SkillEntry> Necromancer { get; } =
    [
        // Basic
        new("Bone Splinters",    0x001C7E6E, "Necromancer", InGameVerified: false),
        new("Decompose",         0x001C7E7D, "Necromancer", InGameVerified: false),
        new("Hemorrhage",        0x001C7E84, "Necromancer", InGameVerified: false),
        new("Reap",              0x001C7E88, "Necromancer", InGameVerified: false),
        // Core
        new("Bone Spear",        0x001C7E90, "Necromancer", InGameVerified: false),
        new("Blight",            0x001C7E9A, "Necromancer", InGameVerified: false),
        new("Sever",             0x001C7EA1, "Necromancer", InGameVerified: false),
        new("Blood Surge",       0x001C7EA8, "Necromancer", InGameVerified: false),
        new("Blood Lance",       0x001C7EB0, "Necromancer", InGameVerified: false),
        // Defensive/Special
        new("Blood Mist",        0x001C7EB2, "Necromancer", InGameVerified: false),
        new("Bone Prison",       0x001C7EB6, "Necromancer", InGameVerified: false),
        new("Bone Spirit",       0x001C7EB8, "Necromancer", InGameVerified: false),
        new("Corpse Explosion",  0x001C7EBA, "Necromancer", InGameVerified: false),
        new("Corpse Tendrils",   0x001C7EBC, "Necromancer", InGameVerified: false),
        new("Decrepify",         0x001C7EBE, "Necromancer", InGameVerified: false),
        new("Iron Maiden",       0x001C7EC0, "Necromancer", InGameVerified: false),
        // Golems/Summoning
        new("Skeleton Warrior",  0x001D6E4F, "Necromancer", InGameVerified: false),
        new("Skeleton Mage",     0x001D6E51, "Necromancer", InGameVerified: false),
        new("Golem",             0x001D6E53, "Necromancer", InGameVerified: false),
        // Categories
        new("Macabre Skills",    0x001D6E47, "Necromancer", InGameVerified: false),
        new("Curse Skills",      0x001D6E49, "Necromancer", InGameVerified: false),
        new("Corpse Skills",     0x001D6E4B, "Necromancer", InGameVerified: false),
        new("Blood Skills",      0x00280B95, "Necromancer", InGameVerified: false),
        new("Bone Skills",       0x00280B97, "Necromancer", InGameVerified: false),
        new("Darkness Skills",   0x00280B99, "Necromancer", InGameVerified: false),
        new("Minion Skills",     0x00280B9B, "Necromancer", InGameVerified: false)
    ];

    // ── Rogue ─────────────────────────────────────────────────────────────────

    public static IReadOnlyList<SkillEntry> Rogue { get; } =
    [
        // Basic
        new("Heartseeker",          0x001C952F, "Rogue", InGameVerified: false),
        new("Puncture",             0x001C9531, "Rogue", InGameVerified: false),
        new("Invigorating Strike",  0x001C9533, "Rogue", InGameVerified: false),
        new("Forceful Arrow",       0x001C9535, "Rogue", InGameVerified: false),
        new("Blade Shift",          0x001C9537, "Rogue", InGameVerified: false),
        // Core
        new("Rapid Fire",           0x001C953B, "Rogue", InGameVerified: false),
        new("Flurry",               0x001C953D, "Rogue", InGameVerified: false),
        new("Barrage",              0x001C953F, "Rogue", InGameVerified: false),
        new("Twisting Blades",      0x001C9541, "Rogue", InGameVerified: false),
        new("Penetrating Shot",     0x001C9543, "Rogue", InGameVerified: false),
        // Defensive/Special
        new("Poison Imbuement",     0x001C9545, "Rogue", InGameVerified: false),
        new("Caltrops",             0x001C9547, "Rogue", InGameVerified: false),
        new("Cold Imbuement",       0x001C9549, "Rogue", InGameVerified: false),
        new("Concealment",          0x001C954B, "Rogue", InGameVerified: false),
        new("Dark Shroud",          0x001C954D, "Rogue", InGameVerified: false),
        new("Dash",                 0x001C954F, "Rogue", InGameVerified: false),
        new("Poison Trap",          0x001C9551, "Rogue", InGameVerified: false),
        new("Shadow Imbuement",     0x001C9553, "Rogue", InGameVerified: false),
        new("Shadow Step",          0x001C9555, "Rogue", InGameVerified: false),
        new("Smoke Bomb",           0x001C9557, "Rogue", InGameVerified: false),
        // Ultimate (Season 7+ additions)
        new("Grenade",              0x002782B1, "Rogue", InGameVerified: false),
        new("Shade",                0x002782B3, "Rogue", InGameVerified: false),
        new("Arrow Storm",          0x002782B5, "Rogue", InGameVerified: false),
        // Categories
        new("Agility Skills",       0x001D6E41, "Rogue", InGameVerified: false),
        new("Subterfuge Skills",    0x001D6E43, "Rogue", InGameVerified: false),
        new("Imbuement Skills",     0x001D6E45, "Rogue", InGameVerified: false),
        new("Cutthroat Skills",     0x00280BA1, "Rogue", InGameVerified: false),
        new("Grenade Skills",       0x00280BA3, "Rogue", InGameVerified: false),
        new("Marksman Skills",      0x00280BA5, "Rogue", InGameVerified: false),
        new("Trap Skills",          0x00280BA7, "Rogue", InGameVerified: false)
    ];

    // ── Sorcerer ──────────────────────────────────────────────────────────────

    public static IReadOnlyList<SkillEntry> Sorcerer { get; } =
    [
        new("Spark",                 0x001D674B, "Sorcerer", InGameVerified: true),
        new("Fire Bolt",             0x001D674D, "Sorcerer", InGameVerified: true),
        new("Frost Bolt",            0x001D6750, "Sorcerer", InGameVerified: true),
        new("Arc Lash",              0x001D6752, "Sorcerer", InGameVerified: true),
        // Core
        new("Fireball",              0x001D673F, "Sorcerer", InGameVerified: false),
        new("Ice Shards",            0x001D6741, "Sorcerer", InGameVerified: false),
        new("Chain Lightning",       0x001D6743, "Sorcerer", InGameVerified: false),
        new("Charged Bolts",         0x001D6745, "Sorcerer", InGameVerified: false),
        new("Incinerate",            0x001D6747, "Sorcerer", InGameVerified: false),
        new("Frozen Orb",            0x001D6749, "Sorcerer", InGameVerified: false),
        // Defensive/Special
        new("Ball Lightning",        0x001D6754, "Sorcerer", InGameVerified: false),
        new("Blizzard",              0x001D6756, "Sorcerer", InGameVerified: false),
        new("Firewall",              0x001D6758, "Sorcerer", InGameVerified: false),
        new("Flame Shield",          0x001D675A, "Sorcerer", InGameVerified: false),
        new("Frost Nova",            0x001D675C, "Sorcerer", InGameVerified: false),
        new("Hydra",                 0x001D675E, "Sorcerer", InGameVerified: false),
        new("Ice Armor",             0x001D6760, "Sorcerer", InGameVerified: false),
        new("Ice Blades",            0x001D6762, "Sorcerer", InGameVerified: false),
        new("Lightning Spear",       0x001D6764, "Sorcerer", InGameVerified: false),
        new("Meteor",                0x001D6766, "Sorcerer", InGameVerified: false),
        new("Teleport",              0x001D6768, "Sorcerer", InGameVerified: false),
        new("Familiar",              0x001E79A8, "Sorcerer", InGameVerified: false),
        // Categories
        new("Conjuration Skills",    0x001D6E3D, "Sorcerer", InGameVerified: false),
        new("Mastery Skills",        0x001D6E3F, "Sorcerer", InGameVerified: false),
        new("Frost Skills",          0x00280BA9, "Sorcerer", InGameVerified: false),
        new("Pyromancy Skills",      0x00280BAB, "Sorcerer", InGameVerified: false),
        new("Shock Skills",          0x00280BAD, "Sorcerer", InGameVerified: false)
    ];

    // ── Spiritborn ────────────────────────────────────────────────────────────

    public static IReadOnlyList<SkillEntry> Spiritborn { get; } =
    [
        // Basic (X1_ prefix = Vessel of Hatred DLC)
        new("Rock Splitter",     0x001EB8A4, "Spiritborn", InGameVerified: false),
        new("Thunderspike",      0x001EBBAD, "Spiritborn", InGameVerified: false),
        new("Thrash",            0x001EBBB7, "Spiritborn", InGameVerified: false),
        new("Withering Fist",    0x001EBD1B, "Spiritborn", InGameVerified: false),
        // Core
        new("Crushing Hand",     0x001EBD36, "Spiritborn", InGameVerified: false),
        new("Quill Volley",      0x001EBD49, "Spiritborn", InGameVerified: false),
        new("Rake",              0x001EBD60, "Spiritborn", InGameVerified: false),
        new("Stinger",           0x001EBDB9, "Spiritborn", InGameVerified: false),
        // Special
        new("Vortex",            0x001EBEE9, "Spiritborn", InGameVerified: false),
        new("Soar",              0x001EC0FE, "Spiritborn", InGameVerified: false),
        new("Ravager",           0x001EC119, "Spiritborn", InGameVerified: false),
        new("Toxic Skin",        0x001EC199, "Spiritborn", InGameVerified: false),
        new("Armored Hide",      0x001EC1B8, "Spiritborn", InGameVerified: false),
        new("Concussive Stomp",  0x001ED05A, "Spiritborn", InGameVerified: false),
        new("Counterattack",     0x001ED0CB, "Spiritborn", InGameVerified: false),
        new("Scourge",           0x001ED1D4, "Spiritborn", InGameVerified: false),
        new("Payback",           0x001ED2E9, "Spiritborn", InGameVerified: false),
        new("Razor Wings",       0x001ED2F1, "Spiritborn", InGameVerified: false),
        new("Rushing Claw",      0x001ED338, "Spiritborn", InGameVerified: false),
        new("Touch of Death",    0x001ED34D, "Spiritborn", InGameVerified: false),
        // Categories (X1_ = base game categories, X2_ = Season 7 guardian categories)
        new("Focus Skills",      0x001EBDFD, "Spiritborn", InGameVerified: false),
        new("Potency Skills",    0x001ED2C8, "Spiritborn", InGameVerified: false),
        new("Centipede Skills",  0x00280BAF, "Spiritborn", InGameVerified: false),
        new("Eagle Skills",      0x00280BB1, "Spiritborn", InGameVerified: false),
        new("Gorilla Skills",    0x00280BB3, "Spiritborn", InGameVerified: false),
        new("Jaguar Skills",     0x00280BB5, "Spiritborn", InGameVerified: false)
    ];

    // ── Paladin ───────────────────────────────────────────────────────────────

    public static IReadOnlyList<SkillEntry> Paladin { get; } =
    [
        // Basic
        new("Advance",              0x002539AB, "Paladin", InGameVerified: false),
        new("Brandish",             0x00253A6C, "Paladin", InGameVerified: false),
        new("Holy Bolt",            0x00261AA5, "Paladin", InGameVerified: false),
        new("Clash",                0x00261AA7, "Paladin", InGameVerified: false),
        // Core
        new("Blessed Shield",       0x0024F051, "Paladin", InGameVerified: false),
        new("Blessed Hammer",       0x0024F057, "Paladin", InGameVerified: false),
        new("Shield Bash",          0x0025122E, "Paladin", InGameVerified: false),
        new("Divine Lance",         0x0025B0E2, "Paladin", InGameVerified: false),
        new("Spear of the Heavens", 0x0025C005, "Paladin", InGameVerified: false),
        new("Zeal",                 0x00261AB7, "Paladin", InGameVerified: false),
        // Auras
        new("Fanaticism Aura",      0x00261ABB, "Paladin", InGameVerified: false),
        new("Holy Light Aura",      0x00261ABD, "Paladin", InGameVerified: false),
        new("Defiance Aura",        0x00261ABF, "Paladin", InGameVerified: false),
        // Valor
        new("Aegis",                0x00261ACE, "Paladin", InGameVerified: false),
        new("Shield Charge",        0x00261AD8, "Paladin", InGameVerified: false),
        new("Falling Star",         0x00261ADD, "Paladin", InGameVerified: false),
        new("Rally",                0x00261AE2, "Paladin", InGameVerified: false),
        // Justice
        new("Consecration",         0x00261AE4, "Paladin", InGameVerified: false),
        new("Purify",               0x00261AE6, "Paladin", InGameVerified: false),
        new("Condemn",              0x00261AE8, "Paladin", InGameVerified: false),
        new("Spear of the Heavens (Justice)", 0x00261AEA, "Paladin", InGameVerified: false),
        // Categories
        new("Aura Skills",          0x00261AC2, "Paladin", InGameVerified: false),
        new("Valor Skills",         0x00261AC7, "Paladin", InGameVerified: false),
        new("Justice Skills",       0x00261ACC, "Paladin", InGameVerified: false),
        new("Judicator Skills",     0x0024F033, "Paladin", InGameVerified: false),
        new("Zealot Skills",        0x0024F06A, "Paladin", InGameVerified: false),
        new("Disciple Skills",      0x00280B9D, "Paladin", InGameVerified: false),
        new("Juggernaut Skills",    0x00280B9F, "Paladin", InGameVerified: false)
    ];

    // ── Warlock ───────────────────────────────────────────────────────────────

    public static IReadOnlyList<SkillEntry> Warlock { get; } =
    [
        // Basic
        new("Command Fallen",      0x0026AD4D, "Warlock", InGameVerified: false),
        new("Molten Bomb",         0x0026AD68, "Warlock", InGameVerified: false),
        new("Doom",                0x0026AD6A, "Warlock", InGameVerified: false),
        new("Hellion Sting",       0x0026AD6C, "Warlock", InGameVerified: false),
        // Core
        new("Hell Fracture",       0x0026AD42, "Warlock", InGameVerified: false),
        new("Umbral Chains",       0x0026AD44, "Warlock", InGameVerified: false),
        new("Blazing Scream",      0x0026AD46, "Warlock", InGameVerified: false),
        new("Dread Claws",         0x0026AD48, "Warlock", InGameVerified: false),
        new("Bombardment",         0x0026ADCD, "Warlock", InGameVerified: false),
        // Defensive/Special
        new("Wall of Agony",       0x0026AD71, "Warlock", InGameVerified: false),
        new("Tortured Wretch",     0x0026AD78, "Warlock", InGameVerified: false),
        new("Dark Prison",         0x0026AD7A, "Warlock", InGameVerified: false),
        new("Nether Step",         0x0026AD7C, "Warlock", InGameVerified: false),
        new("Rampage",             0x0026AD7E, "Warlock", InGameVerified: false),
        new("Infernal Breath",     0x0026AD80, "Warlock", InGameVerified: false),
        new("Tyrant's Grasp",      0x0026AD83, "Warlock", InGameVerified: false),
        new("Profane Sentinel",    0x0026AD85, "Warlock", InGameVerified: false),
        new("Sigil of Subversion", 0x0026AD87, "Warlock", InGameVerified: false),
        new("Sigil of Summons",    0x0026AD89, "Warlock", InGameVerified: false),
        new("Sigil of Chaos",      0x0026AD8B, "Warlock", InGameVerified: false),
        // Categories
        new("Occult Skills",       0x0026ADC2, "Warlock", InGameVerified: false),
        new("Hellfire Skills",     0x0026ADC4, "Warlock", InGameVerified: false),
        new("Abyss Skills",        0x0026ADC6, "Warlock", InGameVerified: false),
        new("Demonology Skills",   0x0026ADC8, "Warlock", InGameVerified: false)
    ];

    // ── Aggregates ────────────────────────────────────────────────────────────

    public static IReadOnlyList<SkillEntry> All { get; } =
        [.. Generic, .. Barbarian, .. Druid, .. Necromancer, .. Rogue, .. Sorcerer,
         .. Spiritborn, .. Paladin, .. Warlock];

    public static IReadOnlyDictionary<uint, SkillEntry> ByHash { get; } =
        All.Where(s => s.Hash != 0)
           .ToDictionary(s => s.Hash);

    public static string GetDisplayName(uint hash)
        => ByHash.TryGetValue(hash, out var entry) ? entry.Name : $"Unknown skill (0x{hash:x8})";
}

namespace D4LootBench.Core.Gear;

/// <summary>Character class; drives class-specific weapon slot roles. <see cref="All"/> means
/// class-agnostic (legacy single-type weapon behavior).</summary>
public enum PlayerClass
{
    /// <summary>Class-agnostic — legacy single-type weapon behavior.</summary>
    All,

    /// <summary>Barbarian.</summary>
    Barbarian,

    /// <summary>Druid.</summary>
    Druid,

    /// <summary>Necromancer.</summary>
    Necromancer,

    /// <summary>Paladin.</summary>
    Paladin,

    /// <summary>Rogue.</summary>
    Rogue,

    /// <summary>Sorcerer.</summary>
    Sorcerer,

    /// <summary>Spiritborn.</summary>
    Spiritborn,

    /// <summary>Warlock.</summary>
    Warlock,
}

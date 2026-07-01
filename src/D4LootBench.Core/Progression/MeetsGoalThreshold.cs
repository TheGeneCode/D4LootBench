namespace D4LootBench.Core.Progression;

/// <summary>How closely an equipped item's affixes must match a slot's target affixes to "meet goal".</summary>
public enum ThresholdMode
{
    /// <summary>Every target affix must be present on the item (extras ignored).</summary>
    ExactMatch,

    /// <summary>At least <see cref="MeetsGoalThreshold.RequiredAffixCount"/> of the M target affixes present.</summary>
    NOfM,

    /// <summary>N target affixes present AND at least K of the matched targets are greater affixes.</summary>
    AffixWithGreaterAffixCount,
}

/// <summary>Configurable "meets goal" threshold. Immutable; reuse presets or build your own.</summary>
public sealed record MeetsGoalThreshold
{
    /// <summary>Gets the match mode. Defaults to <see cref="ThresholdMode.NOfM"/>.</summary>
    public ThresholdMode Mode { get; init; } = ThresholdMode.NOfM;

    /// <summary>Gets the minimum matched target-affix count for <see cref="ThresholdMode.NOfM"/> and
    /// <see cref="ThresholdMode.AffixWithGreaterAffixCount"/>. Effectively capped at the target-set size.</summary>
    public int RequiredAffixCount { get; init; } = 3;

    /// <summary>Gets the minimum number of matched target affixes that must be greater affixes
    /// (only for <see cref="ThresholdMode.AffixWithGreaterAffixCount"/>).</summary>
    public int RequiredGreaterAffixCount { get; init; }

    /// <summary>Gets a threshold requiring all target affixes present.</summary>
    public static MeetsGoalThreshold Exact { get; } = new() { Mode = ThresholdMode.ExactMatch };

    /// <summary>At least <paramref name="n"/> of the target affixes present.</summary>
    /// <param name="n">The minimum matched target-affix count.</param>
    /// <returns>A configured <see cref="MeetsGoalThreshold"/>.</returns>
    public static MeetsGoalThreshold NOf(int n) => new() { Mode = ThresholdMode.NOfM, RequiredAffixCount = n };

    /// <summary>At least <paramref name="affixes"/> target affixes present, of which at least
    /// <paramref name="greaterAffixes"/> are greater affixes.</summary>
    /// <param name="affixes">The minimum matched target-affix count.</param>
    /// <param name="greaterAffixes">The minimum matched target affixes that must be greater affixes.</param>
    /// <returns>A configured <see cref="MeetsGoalThreshold"/>.</returns>
    public static MeetsGoalThreshold WithGreaterAffixes(int affixes, int greaterAffixes) => new()
    {
        Mode = ThresholdMode.AffixWithGreaterAffixCount,
        RequiredAffixCount = affixes,
        RequiredGreaterAffixCount = greaterAffixes,
    };
}

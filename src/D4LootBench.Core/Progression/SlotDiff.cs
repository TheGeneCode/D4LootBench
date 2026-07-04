namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Gear;

/// <summary>Outcome for one slot in a diff.</summary>
public enum SlotDiffStatus
{
    /// <summary>Equipped gear already satisfies the goal — emit NO rule.</summary>
    MeetsGoal,

    /// <summary>Goal exists and is unmet (below threshold, wrong/absent unique, or no gear) — needs a rule.</summary>
    NeedsRule,

    /// <summary>Gear present but no goal defined for the slot — informational, no rule.</summary>
    NoGoal,
}

/// <summary>Per-slot diff detail.</summary>
public sealed record SlotDiff
{
    /// <summary>Gets the slot this diff describes.</summary>
    public required SlotKey Slot { get; init; }

    /// <summary>Gets the outcome.</summary>
    public required SlotDiffStatus Status { get; init; }

    /// <summary>Gets the equipped item, or <c>null</c> when the slot is empty.</summary>
    public GearItem? EquippedItem { get; init; }

    /// <summary>Gets the resolved goal, or <c>null</c> for <see cref="SlotDiffStatus.NoGoal"/>.</summary>
    public SlotGoal? Goal { get; init; }

    /// <summary>Gets the full target affix set (for rule generation in Phase 3).</summary>
    public IReadOnlyList<uint> TargetAffixIds { get; init; } = [];

    /// <summary>Gets the target affixes NOT present on the equipped item.</summary>
    public IReadOnlyList<uint> MissingAffixIds { get; init; } = [];

    /// <summary>Gets how many target affixes matched.</summary>
    public int MatchedAffixCount { get; init; }

    /// <summary>Gets how many matched target affixes were greater affixes.</summary>
    public int MatchedGreaterAffixCount { get; init; }

    /// <summary>Gets the effective target-affix ceiling for the equipped item:
    /// min(target count, rollable affix cap for its rarity). Zero when there are no targets.</summary>
    public int EffectiveTargetCap { get; init; }

    /// <summary>Gets a value indicating whether the equipped item already holds as many target
    /// affixes as it can (matched &gt;= EffectiveTargetCap) — the GA-only upgrade regime. Always
    /// false for an empty slot.</summary>
    public bool IsMaxedOnTargets { get; init; }

    /// <summary>Gets a value indicating whether the unique gate was satisfied (true when no unique required).</summary>
    public bool UniqueSatisfied { get; init; } = true;

    /// <summary>Gets human-readable notes (e.g. "no gear equipped", "wrong unique").</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>Aggregate diff across all slots, with 25-rule-ceiling awareness.</summary>
public sealed record SlotDiffResult
{
    /// <summary>Game-enforced maximum rules per filter (all rule kinds share this budget).</summary>
    public const int MaxRulesPerFilter = 25;

    /// <summary>Gets every evaluated slot, in a stable order.</summary>
    public required IReadOnlyList<SlotDiff> Slots { get; init; }

    /// <summary>Gets the slots that still need a rule.</summary>
    public IReadOnlyList<SlotDiff> SlotsNeedingRules =>
        Slots.Where(s => s.Status == SlotDiffStatus.NeedsRule).ToList();

    /// <summary>Gets the number of slots needing rules (slot-drop rule count).</summary>
    public int RuleCount => SlotsNeedingRules.Count;

    /// <summary>Gets a value indicating whether slot rules alone stay within the filter ceiling.
    /// Other rule kinds (uniques, charms, hide-all) also consume budget in later phases.</summary>
    public bool WithinRuleBudget => RuleCount <= MaxRulesPerFilter;

    /// <summary>Gets the rule budget remaining after slot rules (can go negative).</summary>
    public int RemainingRuleBudget => MaxRulesPerFilter - RuleCount;
}

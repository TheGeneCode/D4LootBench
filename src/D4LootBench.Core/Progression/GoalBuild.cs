namespace D4LootBench.Core.Progression;

/// <summary>Target for one slot: the affixes sought (by hash), an optional mandatory unique, and an
/// optional per-slot threshold override.</summary>
public sealed record SlotGoal
{
    /// <summary>Gets the sought-after affix hash IDs for this slot.</summary>
    public IReadOnlyList<uint> TargetAffixIds { get; init; } = [];

    /// <summary>Gets the required unique-item hash. When set, the equipped item must be this unique
    /// (a mandatory "special affix") in addition to meeting the affix threshold.</summary>
    public uint? TargetUnique { get; init; }

    /// <summary>Gets a per-slot threshold override; <c>null</c> uses the engine default.</summary>
    public MeetsGoalThreshold? Threshold { get; init; }
}

/// <summary>A target build: per-slot goals keyed by <see cref="SlotKey"/>. Lookup falls back from an
/// exact <c>(Slot, Ordinal, Role)</c> key to the ordinal-stripped default <c>(Slot, 0, Role)</c> — so a
/// goal authored once for a slot/role applies to all its instances (e.g. Ring#1 → Ring#0) unless a more
/// specific key overrides it.</summary>
public sealed class GoalBuild
{
    private readonly IReadOnlyDictionary<SlotKey, SlotGoal> _goals;

    /// <summary>Initializes a new instance of the <see cref="GoalBuild"/> class.</summary>
    /// <param name="goals">The per-slot goals keyed by <see cref="SlotKey"/>.</param>
    /// <param name="name">An optional display name.</param>
    public GoalBuild(IReadOnlyDictionary<SlotKey, SlotGoal> goals, string? name = null)
    {
        _goals = goals;
        Name = name;
    }

    /// <summary>Gets an optional display name.</summary>
    public string? Name { get; }

    /// <summary>Gets all authored goals.</summary>
    public IReadOnlyDictionary<SlotKey, SlotGoal> Goals => _goals;

    /// <summary>Resolves the goal for a slot: exact <c>(Slot, Ordinal, Role)</c> key first, then the
    /// ordinal-stripped <c>(Slot, 0, Role)</c> default.</summary>
    /// <param name="key">The slot key to resolve.</param>
    /// <returns>The resolved goal, or <c>null</c> when none applies.</returns>
    public SlotGoal? Lookup(SlotKey key)
    {
        if (_goals.TryGetValue(key, out var exact))
        {
            return exact;
        }

        // Strip the ordinal — role/slot default (Slot, 0, Role). Covers Ring#1 → Ring#0 and any ordinal>0 role slot.
        return key.Ordinal != 0 && _goals.TryGetValue(new SlotKey(key.Slot, 0, key.Role), out var slotDefault)
            ? slotDefault
            : null;
    }
}

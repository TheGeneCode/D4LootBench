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
/// exact key, to the weapon-family default (item-type discriminator stripped), to the slot-type default
/// <c>(Slot,0)</c> (ordinal stripped) — so a goal authored once for a slot applies to all its instances
/// (including every concrete weapon type) unless a more specific key overrides it.</summary>
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

    /// <summary>Resolves the goal for a slot: exact key first, then the weapon-family default (item-type
    /// discriminator stripped), then the <c>(Slot,0)</c> slot-type default (ordinal stripped).</summary>
    /// <param name="key">The slot key to resolve.</param>
    /// <returns>The resolved goal, or <c>null</c> when none applies.</returns>
    public SlotGoal? Lookup(SlotKey key)
    {
        if (_goals.TryGetValue(key, out var exact))
        {
            return exact;
        }

        // Strip the item-type discriminator — weapon-family default (Slot, Ordinal, null).
        if (key.ItemType is not null &&
            _goals.TryGetValue(new SlotKey(key.Slot, key.Ordinal), out var familyGoal))
        {
            return familyGoal;
        }

        // Strip the ordinal — slot-type default (Slot, 0). Covers Ring#2 and ordinal>0 weapons.
        return key.Ordinal != 0 && _goals.TryGetValue(new SlotKey(key.Slot), out var slotDefault)
            ? slotDefault
            : null;
    }
}

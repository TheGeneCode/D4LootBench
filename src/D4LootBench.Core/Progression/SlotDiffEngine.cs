namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Gear;

/// <summary>Computes a slot-drop diff: which slots already meet the goal (emit no rule) vs. still need one.</summary>
public sealed class SlotDiffEngine
{
    private readonly MeetsGoalThreshold _defaultThreshold;

    /// <summary>Initializes a new instance of the <see cref="SlotDiffEngine"/> class with a default
    /// threshold (used when a <see cref="SlotGoal"/> supplies none). Defaults to 3-of-M.</summary>
    /// <param name="defaultThreshold">The default threshold, or <c>null</c> for 3-of-M.</param>
    public SlotDiffEngine(MeetsGoalThreshold? defaultThreshold = null)
        => _defaultThreshold = defaultThreshold ?? MeetsGoalThreshold.NOf(3);

    /// <summary>Diffs current gear against a goal build.</summary>
    /// <param name="loadout">The player's current gear.</param>
    /// <param name="goal">The target build.</param>
    /// <returns>The aggregate slot diff.</returns>
    public SlotDiffResult Diff(EquippedLoadout loadout, GoalBuild goal)
    {
        var keys = loadout.Slots.Concat(goal.Goals.Keys).Distinct()
            .OrderBy(k => k.Slot).ThenBy(k => k.Ordinal).ThenBy(k => k.Role);

        var diffs = new List<SlotDiff>();
        foreach (var key in keys)
        {
            var item = loadout[key];
            var slotGoal = goal.Lookup(key);

            if (slotGoal is null)
            {
                if (item is not null)
                {
                    diffs.Add(new SlotDiff { Slot = key, Status = SlotDiffStatus.NoGoal, EquippedItem = item });
                }

                continue; // no goal + no gear => nothing to report
            }

            diffs.Add(Evaluate(key, item, slotGoal));
        }

        return new SlotDiffResult { Slots = diffs };
    }

    /// <summary>Folds an item's resolved affixes into a hash → anyGreater map (unresolved hashes skipped).</summary>
    private static Dictionary<uint, bool> BuildPresentMap(GearItem? item)
    {
        var present = new Dictionary<uint, bool>();
        if (item is null)
        {
            return present;
        }

        foreach (var affix in item.Affixes)
        {
            if (affix.AffixHash is not { } hash)
            {
                continue;
            }

            present[hash] = (present.TryGetValue(hash, out var wasGreater) && wasGreater) || affix.IsGreaterAffix;
        }

        return present;
    }

    private SlotDiff Evaluate(SlotKey key, GearItem? item, SlotGoal slotGoal)
    {
        var threshold = slotGoal.Threshold ?? _defaultThreshold;

        // Target affixes are a distinct set — a D4 item cannot roll the same affix twice. Dedupe
        // (preserving priority order) so a repeated hash isn't double-counted as a match, which
        // would inflate MatchedAffixCount and, downstream, the generated rule's required count.
        var targets = slotGoal.TargetAffixIds.Distinct().ToList();
        var notes = new List<string>();

        var present = BuildPresentMap(item);
        var matched = targets.Where(present.ContainsKey).ToList();
        var missing = targets.Where(t => !present.ContainsKey(t)).ToList();
        var matchedGa = matched.Count(t => present[t]);

        // Unique gate: when required, item must be that unique. An empty slot is covered by the
        // shared "no gear equipped" note below, so only flag a present-but-wrong unique here.
        var uniqueSatisfied = slotGoal.TargetUnique is not { } wanted || item?.UniqueHash == wanted;
        if (slotGoal.TargetUnique is not null && !uniqueSatisfied && item is not null)
        {
            notes.Add("wrong or missing unique");
        }

        if (item is null)
        {
            notes.Add("no gear equipped");
        }

        var affixOk = threshold.Mode switch
        {
            ThresholdMode.ExactMatch => missing.Count == 0,
            ThresholdMode.NOfM => matched.Count >= Math.Min(threshold.RequiredAffixCount, targets.Count),
            ThresholdMode.AffixWithGreaterAffixCount =>
                matched.Count >= Math.Min(threshold.RequiredAffixCount, targets.Count)
                && matchedGa >= threshold.RequiredGreaterAffixCount,
            _ => false,
        };

        var meets = item is not null && uniqueSatisfied && affixOk;

        return new SlotDiff
        {
            Slot = key,
            Status = meets ? SlotDiffStatus.MeetsGoal : SlotDiffStatus.NeedsRule,
            EquippedItem = item,
            Goal = slotGoal,
            TargetAffixIds = targets,
            MissingAffixIds = missing,
            MatchedAffixCount = matched.Count,
            MatchedGreaterAffixCount = matchedGa,
            UniqueSatisfied = uniqueSatisfied,
            Notes = notes,
        };
    }
}

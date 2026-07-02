namespace D4LootBench.Core.Models;

/// <summary>Deterministic construction of a per-slot filter rule (optional item-type gate +
/// an affix requirement) from already-resolved hash IDs. Shared by build-guide import and
/// progression generation so both emit byte-identical rule shapes.</summary>
public static class SlotRuleBuilder
{
    /// <summary>Builds a slot rule. Returns <c>null</c> when no conditions could be formed
    /// (no item type and no affixes).</summary>
    /// <param name="name">Rule display name.</param>
    /// <param name="visibility">Rule visibility.</param>
    /// <param name="color">Packed ABGR color.</param>
    /// <param name="itemTypeHash">Item-type hash to gate on, or <c>null</c> to skip the gate.</param>
    /// <param name="affixIds">Target affix hashes (already trimmed/ordered by the caller).</param>
    /// <param name="greaterAffixIds">Subset of <paramref name="affixIds"/> to flag as greater.</param>
    /// <param name="requiredCount">Minimum affixes required; clamped to [1, affix count].</param>
    /// <returns>The assembled rule, or <c>null</c> when no conditions could be formed.</returns>
    public static FilterRule? Build(
        string name,
        Visibility visibility,
        uint color,
        uint? itemTypeHash,
        IReadOnlyList<uint> affixIds,
        IReadOnlyCollection<uint> greaterAffixIds,
        int requiredCount)
    {
        var conditions = new List<Condition>();

        if (itemTypeHash is { } typeHash)
        {
            conditions.Add(new ItemTypeCondition([typeHash]));
        }

        if (affixIds.Count > 0)
        {
            var capped = affixIds.Take(AffixCondition.MaxSelectionCount).ToList();
            var greaterEntries = capped
                .Where(greaterAffixIds.Contains)
                .Select(id => new GreaterAffixEntry(id, id))
                .ToList();
            var required = Math.Clamp(requiredCount, 1, capped.Count);
            conditions.Add(new AffixCondition(capped, required) { GreaterEntries = greaterEntries });
        }

        return conditions.Count == 0 ? null : new FilterRule(name, visibility, color, conditions);
    }
}

namespace D4LootBench.Core.Models;

/// <summary>Deterministic construction of a per-slot filter rule (optional item-type gate +
/// an affix requirement) from already-resolved hash IDs. Shared by build-guide import and
/// progression generation so both emit byte-identical rule shapes.</summary>
public static class SlotRuleBuilder
{
    /// <summary>Maximum rule-name length D4 accepts. Longer names are silently blanked by the game
    /// (the rule then renders as "Rule #N"), so generated names are truncated to this length. Mirrors
    /// the limit applied to LLM-authored rule names in the AI assistant.</summary>
    public const int MaxNameLength = 24;

    /// <summary>Builds a slot rule gating on a single item-type hash. Delegates to
    /// <see cref="BuildMultiType"/>. Returns <c>null</c> when no conditions could be formed (no item type
    /// and no affixes).</summary>
    /// <param name="name">Rule display name; truncated to <see cref="MaxNameLength"/> characters.</param>
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
        => BuildMultiType(
            name,
            visibility,
            color,
            itemTypeHash is { } h ? new[] { h } : null,
            affixIds,
            greaterAffixIds,
            requiredCount);

    /// <summary>Builds a slot rule gating on a SET of item-type hashes (a single
    /// <see cref="ItemTypeCondition"/> listing them all). Returns <c>null</c> when no conditions could be
    /// formed (no item types and no affixes).</summary>
    /// <param name="name">Rule display name; truncated to <see cref="MaxNameLength"/> characters.</param>
    /// <param name="visibility">Rule visibility.</param>
    /// <param name="color">Packed ABGR color.</param>
    /// <param name="typeHashes">Item-type hashes to gate on, or <c>null</c>/empty to skip the gate.</param>
    /// <param name="affixIds">Target affix hashes (already trimmed/ordered by the caller).</param>
    /// <param name="greaterAffixIds">Subset of <paramref name="affixIds"/> to flag as greater.</param>
    /// <param name="requiredCount">Minimum affixes required; clamped to [1, affix count].</param>
    /// <returns>The assembled rule, or <c>null</c> when no conditions could be formed.</returns>
    public static FilterRule? BuildMultiType(
        string name,
        Visibility visibility,
        uint color,
        IReadOnlyCollection<uint>? typeHashes,
        IReadOnlyList<uint> affixIds,
        IReadOnlyCollection<uint> greaterAffixIds,
        int requiredCount)
    {
        var conditions = new List<Condition>();

        if (typeHashes is { Count: > 0 })
        {
            conditions.Add(new ItemTypeCondition(typeHashes.ToList()));
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

        if (conditions.Count == 0)
        {
            return null;
        }

        return new FilterRule(CapName(name), visibility, color, conditions);
    }

    /// <summary>Truncates a rule name to <see cref="MaxNameLength"/> without producing a blank name or
    /// splitting a UTF-16 surrogate pair. Leading/trailing whitespace is trimmed first so the cut anchors
    /// on real content (a blank name reintroduces the "Rule #N" bug this truncation exists to prevent);
    /// the cut then backs off one unit if it would orphan a surrogate.</summary>
    private static string CapName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length <= MaxNameLength)
        {
            return trimmed;
        }

        // Never cut between a high surrogate and its low partner — the orphan is mangled on UTF-8 encode.
        var cut = char.IsHighSurrogate(trimmed[MaxNameLength - 1]) ? MaxNameLength - 1 : MaxNameLength;
        return trimmed[..cut].TrimEnd();
    }
}

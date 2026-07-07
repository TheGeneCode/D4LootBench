namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Models;

/// <summary>Merges three ordered rule blocks into one native filter for export: an override block (highest
/// priority — e.g. "always show mythics"), the generated better-gear block, and an overridden-by block
/// (lowest priority before the catch-all — e.g. "never show rares"). Emits exactly one trailing
/// <see cref="Visibility.HideAll"/> rule and enforces the 25-rule cap by dropping the lowest-priority
/// better-gear rules first (never the user's static rules). <see cref="FilterRuleset.Rules"/> index 0 is
/// highest priority (first-match-wins); see docs/filter-format.md and the strategy doc for the ordering model.</summary>
public sealed class ProgressionFilterMerger
{
    private static readonly uint HideAllColor = FilterRule.PackColor(0, 0, 0);

    /// <summary>Merges the override, better-gear, and overridden-by blocks into one ruleset.</summary>
    /// <param name="overrideBlock">Static rules that take priority over better-gear (placed first). Any
    /// Hide-All rule here is dropped with a warning — it would hide every rule below it.</param>
    /// <param name="betterGear">The generated better-gear ruleset; its single trailing Hide-All is stripped.</param>
    /// <param name="overriddenByBlock">Static rules better-gear takes priority over (placed after better-gear,
    /// before the final catch-all). A trailing Hide-All becomes the final catch-all; any interior Hide-All is
    /// dropped with a warning (it would hide every rule below it).</param>
    /// <param name="filterName">The merged filter's display name.</param>
    /// <returns>The merged ruleset plus warnings and an over-budget flag.</returns>
    public MergedFilterResult Merge(
        IReadOnlyList<FilterRule> overrideBlock,
        FilterRuleset betterGear,
        IReadOnlyList<FilterRule> overriddenByBlock,
        string filterName = "Progression Filter")
    {
        var warnings = new List<string>();

        // Override block: drop any Hide-All (would hide everything below, including better-gear).
        var overrideRules = new List<FilterRule>();
        foreach (var rule in overrideBlock)
        {
            if (rule.Visibility == Visibility.HideAll)
            {
                warnings.Add($"Dropped Hide-All rule \"{rule.Name}\" from the override block — it would hide everything below it.");
                continue;
            }

            overrideRules.Add(rule);
        }

        // Better-gear body = the generated rules minus the single trailing Hide-All the generator appends.
        var betterBody = betterGear.Rules.ToList();
        if (betterBody.Count > 0 && betterBody[^1].Visibility == Visibility.HideAll)
        {
            betterBody.RemoveAt(betterBody.Count - 1);
        }

        // Overridden-by block: a trailing Hide-All becomes the final catch-all. Any *interior* Hide-All is
        // dropped with a warning — it would hide every rule below it (the same hazard the override block
        // guards against) and would leave the merged filter with more than one Hide-All.
        var overriddenSource = overriddenByBlock.ToList();
        FilterRule? overriddenCatchAll = null;
        if (overriddenSource.Count > 0 && overriddenSource[^1].Visibility == Visibility.HideAll)
        {
            overriddenCatchAll = overriddenSource[^1];
            overriddenSource.RemoveAt(overriddenSource.Count - 1);
        }

        var overriddenRules = new List<FilterRule>();
        foreach (var rule in overriddenSource)
        {
            if (rule.Visibility == Visibility.HideAll)
            {
                warnings.Add($"Dropped Hide-All rule \"{rule.Name}\" from the overridden-by block — it would hide everything below it.");
                continue;
            }

            overriddenRules.Add(rule);
        }

        // Exactly one catch-all is always emitted: the block's own trailing Hide-All when present, else a fresh one.
        var reserved = overrideRules.Count + overriddenRules.Count + 1;

        var betterBudget = FilterRuleset.MaxRuleCount - reserved;
        if (betterBudget < 0)
        {
            warnings.Add($"Static blocks use {reserved} rules, exceeding the {FilterRuleset.MaxRuleCount}-rule limit — no better-gear rules were kept and the filter is over budget.");
            betterBudget = 0;
        }

        if (betterBody.Count > betterBudget)
        {
            foreach (var dropped in betterBody.Skip(betterBudget))
            {
                warnings.Add($"Rule budget exceeded — dropped better-gear rule \"{dropped.Name}\".");
            }

            betterBody = betterBody.Take(betterBudget).ToList();
        }

        var merged = new List<FilterRule>(reserved + betterBody.Count);
        merged.AddRange(overrideRules);
        merged.AddRange(betterBody);
        merged.AddRange(overriddenRules);
        merged.Add(overriddenCatchAll ?? new FilterRule("Hide All", Visibility.HideAll, HideAllColor, []));

        var ruleset = new FilterRuleset(filterName, merged);
        return new MergedFilterResult
        {
            Ruleset = ruleset,
            Warnings = warnings,
            OverBudget = merged.Count > FilterRuleset.MaxRuleCount,
        };
    }
}

/// <summary>Result of merging the three rule blocks: the merged ruleset, warnings, and whether the
/// merged rule count exceeds the 25-rule limit (the caller should surface a validation error).</summary>
public sealed record MergedFilterResult
{
    /// <summary>Gets the merged ruleset ready for encoding.</summary>
    public required FilterRuleset Ruleset { get; init; }

    /// <summary>Gets human-readable warnings (dropped override Hide-Alls, budget-dropped better-gear rules).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Gets a value indicating whether the merged filter still exceeds the 25-rule limit
    /// (only possible when the static blocks alone are too large).</summary>
    public bool OverBudget { get; init; }
}

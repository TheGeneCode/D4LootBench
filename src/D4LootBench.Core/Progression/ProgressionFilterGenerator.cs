namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Models;

/// <summary>Deterministically turns a <see cref="SlotDiffResult"/> into a native D4 loot filter:
/// one base rule per incomplete slot, completed slots dropped, and the freed rule budget spent on
/// stricter graded rules for the remaining slots. No LLM involvement.</summary>
public sealed class ProgressionFilterGenerator(NameResolver nameResolver)
{
    private const int BaseRequired = 2;
    private const int StrictRequired = 3;
    private const int StrictGreaterAffixCount = 1;

    // Include every ranked guide affix the game will accept in one Required Affixes condition (D4's
    // hard limit). A progression rule matches "N of these targets", so more targets is more inclusive
    // — capping lower silently dropped the lowest-ranked guide affixes. SlotRuleBuilder re-clamps.
    private const int MaxAffixesPerRule = AffixCondition.MaxSelectionCount;

    private static readonly uint ColorBase = FilterRule.PackColor(255, 180, 0);   // gold — candidate
    private static readonly uint ColorStrict = FilterRule.PackColor(255, 90, 0);  // orange — upgrade / stricter
    private static readonly uint ColorUnique = FilterRule.PackColor(160, 32, 240); // purple
    private static readonly uint ColorHideAll = FilterRule.PackColor(0, 0, 0);    // black

    /// <summary>Generates a progression filter from a slot diff.</summary>
    /// <param name="diff">The slot diff to render into rules.</param>
    /// <param name="filterName">The filter display name.</param>
    /// <returns>The generated ruleset plus warnings and rule counts.</returns>
    public ProgressionFilterResult Generate(SlotDiffResult diff, string filterName = "Progression Filter")
    {
        var needy = diff.SlotsNeedingRules;
        var warnings = new List<string>();

        // Uniques rule: collect distinct target uniques across needy slots.
        var uniqueIds = needy
            .Select(s => s.Goal?.TargetUnique)
            .Where(u => u is not null)
            .Select(u => u!.Value)
            .Distinct()
            .ToList();

        FilterRule? uniqueRule = null;
        if (uniqueIds.Count > 0)
        {
            if (uniqueIds.Count > SpecificUniqueCondition.MaxSelectionCount)
            {
                warnings.Add($"More than {SpecificUniqueCondition.MaxSelectionCount} target uniques — keeping the first {SpecificUniqueCondition.MaxSelectionCount}");
                uniqueIds = uniqueIds.Take(SpecificUniqueCondition.MaxSelectionCount).ToList();
            }

            uniqueRule = new FilterRule("Target Uniques", Visibility.Show, ColorUnique,
                [new SpecificUniqueCondition(uniqueIds)]);
        }

        var mandatory = 1 /* Hide All */ + (uniqueRule is not null ? 1 : 0);
        var capacity = FilterRuleset.MaxRuleCount - mandatory;

        // Budget allocation.
        bool budgetExceeded;
        int strictCount;
        IReadOnlyList<SlotDiff> activeSlots;

        if (needy.Count > capacity)
        {
            budgetExceeded = true;
            strictCount = 0;
            activeSlots = needy.Take(capacity).ToList();
            foreach (var dropped in needy.Skip(capacity))
            {
                warnings.Add($"Rule budget exceeded — dropped {dropped.Slot}");
            }
        }
        else
        {
            budgetExceeded = false;
            activeSlots = needy;
            var slack = capacity - needy.Count;
            strictCount = Math.Min(slack, needy.Count);
        }

        var slotRules = new List<FilterRule>();
        var slotRuleCount = 0;

        for (var i = 0; i < activeSlots.Count; i++)
        {
            var slot = activeSlots[i];
            var itemTypeHash = ResolveItemTypeHash(slot.Slot.Slot);
            if (itemTypeHash is null && slot.Slot.Slot is GearSlot.Weapon or GearSlot.Offhand or GearSlot.Unknown)
            {
                warnings.Add($"Ambiguous item type for {slot.Slot} — emitting affix-only rule");
            }

            var affixIds = slot.TargetAffixIds.Take(MaxAffixesPerRule).ToList();

            var baseRule = SlotRuleBuilder.Build(
                slot.Slot.ToString(), Visibility.Recolor, ColorBase, itemTypeHash,
                affixIds, greaterAffixIds: [], requiredCount: BaseRequired);

            if (baseRule is null)
            {
                warnings.Add($"No conditions for {slot.Slot}");
                continue;
            }

            slotRules.Add(baseRule);
            slotRuleCount++;

            if (i < strictCount)
            {
                var strictRule = SlotRuleBuilder.Build(
                    $"{slot.Slot} (Greater)", Visibility.Show, ColorStrict, itemTypeHash,
                    affixIds, greaterAffixIds: [], requiredCount: StrictRequired);

                if (strictRule is not null)
                {
                    strictRule.Conditions.Add(new GreaterAffixCondition(StrictGreaterAffixCount));
                    slotRules.Add(strictRule);
                    slotRuleCount++;
                }
            }
        }

        var rules = new List<FilterRule>();
        if (uniqueRule is not null)
        {
            rules.Add(uniqueRule);
        }

        rules.AddRange(slotRules);
        rules.Add(new FilterRule("Hide All", Visibility.HideAll, ColorHideAll, []));

        var ruleset = new FilterRuleset(filterName, rules);

        return new ProgressionFilterResult
        {
            Ruleset = ruleset,
            Warnings = warnings,
            SlotRuleCount = slotRuleCount,
            TotalRuleCount = ruleset.Rules.Count,
            BudgetExceeded = budgetExceeded,
        };
    }

    private uint? ResolveItemTypeHash(GearSlot slot)
    {
        var name = slot switch
        {
            GearSlot.Helm => "Helm",
            GearSlot.ChestArmor => "Chest Armor",
            GearSlot.Gloves => "Gloves",
            GearSlot.Pants => "Pants",
            GearSlot.Boots => "Boots",
            GearSlot.Amulet => "Amulet",
            GearSlot.Ring => "Ring",
            _ => null, // Weapon/Offhand/Unknown are ambiguous — no single item type
        };

        return name is not null && nameResolver.TryResolveItemType(name, out var hash, out _)
            ? hash
            : null;
    }
}

/// <summary>Result of generating a progression filter: the ruleset, warnings, and rule counts.</summary>
public sealed record ProgressionFilterResult
{
    /// <summary>Gets the generated ruleset.</summary>
    public required FilterRuleset Ruleset { get; init; }

    /// <summary>Gets human-readable warnings (dropped slots, ambiguous types, capped uniques).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Gets the number of base + strict rules emitted for slots.</summary>
    public int SlotRuleCount { get; init; }

    /// <summary>Gets the total rule count, including the uniques rule and the hide-all fallback.</summary>
    public int TotalRuleCount { get; init; }

    /// <summary>Gets a value indicating whether there were more needy slots than the 25-rule ceiling allows.</summary>
    public bool BudgetExceeded { get; init; }
}

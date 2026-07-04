namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Models;

/// <summary>Deterministically turns a <see cref="SlotDiffResult"/> into a native D4 loot filter, emitting
/// exactly one Recolor rule per needy slot: a GOLD rule for a slot that is not yet maxed on its target
/// affixes (highlights any item holding the SAME OR MORE ranked target affixes than the equipped piece),
/// or a CYAN rule for a slot already maxed on target affixes (highlights items with at least as many
/// Greater Affixes as the equipped piece — the only upgrade a static filter can still catch). Plus a
/// Target Uniques rule and a Hide All fallback. Weapon/offhand slots resolve their class-aware role to a
/// SET of item types (one multi-type <see cref="ItemTypeCondition"/>); rules whose conditions are
/// byte-identical collapse to a single rule (e.g. Barbarian dual-1H hands). Gold rules rank above cyan
/// rules so the lower-value maxed-GA rules are the first to drop under the 25-rule cap. Completed slots
/// (equipped item already meets the goal) are dropped. No LLM involvement.</summary>
/// <param name="nameResolver">The shared fuzzy name resolver.</param>
/// <param name="roleMap">The class-aware weapon role map.</param>
public sealed class ProgressionFilterGenerator(NameResolver nameResolver, WeaponRoleMap roleMap)
{
    private static readonly uint ColorImprovement = FilterRule.PackColor(255, 180, 0); // gold — an affix-count upgrade
    private static readonly uint ColorGreater = FilterRule.PackColor(0, 220, 255);      // cyan — a Greater-Affix upgrade
    private static readonly uint ColorUnique = FilterRule.PackColor(160, 32, 240);      // purple
    private static readonly uint ColorHideAll = FilterRule.PackColor(0, 0, 0);          // black

    /// <summary>Generates a progression filter from a slot diff.</summary>
    /// <param name="diff">The slot diff to render into rules.</param>
    /// <param name="cls">The player class (drives class-aware weapon slot roles).</param>
    /// <param name="filterName">The filter display name.</param>
    /// <returns>The generated ruleset plus warnings and rule counts.</returns>
    public ProgressionFilterResult Generate(
        SlotDiffResult diff,
        PlayerClass cls = PlayerClass.All,
        string filterName = "Progression Filter")
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

            // Must be Recolor, not Show: in-game only Recolor rules apply their color, so Show would
            // silently drop the ColorUnique purple highlight (see plan-notes known-defect section).
            uniqueRule = new FilterRule("Target Uniques", Visibility.Recolor, ColorUnique,
                [new SpecificUniqueCondition(uniqueIds)]);
        }

        var mandatory = 1 /* Hide All */ + (uniqueRule is not null ? 1 : 0);
        var capacity = FilterRuleset.MaxRuleCount - mandatory;

        // Build rules for EVERY needy slot first (slot order), then collapse byte-identical rules, then
        // apply the 25-rule cap. Collapsing first means dual-1H hands sharing a shape don't each burn budget.
        // Each slot contributes exactly ONE rule, keyed on IsMaxedOnTargets: a gold "same-or-more target
        // affixes" rule while the equipped piece can still gain a target affix, or a cyan "same-or-more
        // Greater Affixes" rule once it is maxed on target affixes for its rarity. The two tiers are kept
        // in separate lists so gold rules rank ABOVE cyan rules in the final ruleset — under the cap the
        // lower-value maxed-GA rules are dropped first.
        var baseRules = new List<FilterRule>();
        var greaterRules = new List<FilterRule>();
        foreach (var slot in needy)
        {
            var typeHashes = ResolveTypeHashes(slot.Slot, cls);
            if (typeHashes.Count == 0 && slot.Slot.Slot is GearSlot.Weapon or GearSlot.Offhand or GearSlot.Unknown)
            {
                warnings.Add($"Ambiguous item type for {slot.Slot} — emitting affix-only rule");
            }

            FilterRule? rule;
            if (!slot.IsMaxedOnTargets)
            {
                // Gold: highlight any item with the SAME OR MORE ranked target affixes than equipped
                // (min 1 for an empty/zero-match slot). SlotRuleBuilder clamps required to the affix
                // count, so a fully-matched-but-not-capped slot still yields a satisfiable rule.
                var requiredCount = Math.Max(1, slot.MatchedAffixCount);
                rule = SlotRuleBuilder.BuildMultiType(
                    slot.Slot.ToString(), Visibility.Recolor, ColorImprovement, typeHashes,
                    slot.TargetAffixIds, greaterAffixIds: [], requiredCount: requiredCount);

                if (rule is null)
                {
                    warnings.Add($"No conditions for {slot.Slot}");
                    continue;
                }

                baseRules.Add(rule);
            }
            else
            {
                // Cyan: equipped is maxed on target affixes for its rarity — the only catchable upgrade
                // is more Greater Affixes. Require the same target-affix count plus at least as many GAs
                // as equipped (min 1). GreaterAffixCondition counts GAs across all affixes (D4 primitive),
                // so this is a deliberate approximation of "more GAs on the target affixes".
                rule = SlotRuleBuilder.BuildMultiType(
                    slot.Slot + " (Greater)", Visibility.Recolor, ColorGreater, typeHashes,
                    slot.TargetAffixIds, greaterAffixIds: [], requiredCount: slot.EffectiveTargetCap);

                if (rule is null)
                {
                    warnings.Add($"No conditions for {slot.Slot}");
                    continue;
                }

                rule.Conditions.Add(new GreaterAffixCondition(Math.Max(1, slot.MatchedGreaterAffixCount)));
                greaterRules.Add(rule);
            }
        }

        // Collapse rules whose conditions are byte-identical (same type set + affix requirement + GA count).
        // Base rules come first so, when a base and greater rule ever share a shape, the base keeps its name;
        // first occurrence in this order wins, later duplicates are dropped.
        var seen = new HashSet<string>();
        var collapsed = baseRules.Concat(greaterRules).Where(r => seen.Add(ShapeKey(r))).ToList();

        // Apply the 25-rule cap on the collapsed set; drop the lowest-priority survivors if we exceed it.
        var budgetExceeded = collapsed.Count > capacity;
        List<FilterRule> slotRules;
        if (budgetExceeded)
        {
            slotRules = collapsed.Take(capacity).ToList();
            foreach (var dropped in collapsed.Skip(capacity))
            {
                warnings.Add($"Rule budget exceeded — dropped {dropped.Name}");
            }
        }
        else
        {
            slotRules = collapsed;
        }

        var slotRuleCount = slotRules.Count;

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

    // Ordered, delimited digest of a rule's conditions — the "shape" used to collapse duplicate rules.
    // Excludes the rule name; includes visibility, color, ordered type hashes, each affix condition's
    // ordered affix ids + minimum count + ordered greater ids, and each greater-affix check's minimum count.
    private static string ShapeKey(FilterRule rule)
    {
        var parts = new List<string> { $"v{(int)rule.Visibility}", $"c{rule.Color}" };
        foreach (var condition in rule.Conditions)
        {
            switch (condition)
            {
                case ItemTypeCondition it:
                    parts.Add("t:" + string.Join(",", it.TypeIds));
                    break;
                case AffixCondition af:
                    parts.Add($"a:{string.Join(",", af.AffixIds)}|m{af.MinimumCount}|g{string.Join(",", af.GreaterEntries.Select(e => e.AffixId))}");
                    break;
                case GreaterAffixCondition ga:
                    parts.Add($"ga:{ga.MinimumCount}");
                    break;
                default:
                    parts.Add("?" + condition.GetType().Name);
                    break;
            }
        }

        return string.Join(";", parts);
    }

    // Resolves a slot key to the SET of item-type hashes its rule should gate on. Weapon/offhand roles
    // expand to their class-aware type set (may be empty → affix-only rule); non-weapon slots map to a
    // single armor/accessory type hash.
    private IReadOnlyList<uint> ResolveTypeHashes(SlotKey key, PlayerClass cls)
    {
        if (key.Role != WeaponSlotRole.None)
        {
            return roleMap.AllowedTypeHashes(key.Role, cls);
        }

        var name = key.Slot switch
        {
            GearSlot.Helm => "Helm",
            GearSlot.ChestArmor => "Chest Armor",
            GearSlot.Gloves => "Gloves",
            GearSlot.Pants => "Pants",
            GearSlot.Boots => "Boots",
            GearSlot.Amulet => "Amulet",
            GearSlot.Ring => "Ring",
            _ => null, // Weapon/Offhand with no role, or Unknown — no gate
        };

        return name is not null && nameResolver.TryResolveItemType(name, out var hash, out _)
            ? [hash]
            : [];
    }
}

/// <summary>Result of generating a progression filter: the ruleset, warnings, and rule counts.</summary>
public sealed record ProgressionFilterResult
{
    /// <summary>Gets the generated ruleset.</summary>
    public required FilterRuleset Ruleset { get; init; }

    /// <summary>Gets human-readable warnings (dropped slots, ambiguous types, capped uniques).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Gets the number of per-slot Recolor rules emitted (one per incomplete slot).</summary>
    public int SlotRuleCount { get; init; }

    /// <summary>Gets the total rule count, including the uniques rule and the hide-all fallback.</summary>
    public int TotalRuleCount { get; init; }

    /// <summary>Gets a value indicating whether there were more needy slots than the 25-rule ceiling allows.</summary>
    public bool BudgetExceeded { get; init; }
}

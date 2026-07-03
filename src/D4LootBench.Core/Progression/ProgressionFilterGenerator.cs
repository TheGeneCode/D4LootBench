namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Models;

/// <summary>Deterministically turns a <see cref="SlotDiffResult"/> into a native D4 loot filter:
/// a gold Recolor rule per incomplete slot that highlights any item with more of the ranked target
/// affixes than the equipped piece, optionally a cyan Greater-Affix companion rule that catches items
/// with the SAME target-affix count but more Greater Affixes, plus a Target Uniques rule and a Hide All
/// fallback. Weapon/offhand slots resolve their class-aware role to a SET of item types (one multi-type
/// <see cref="ItemTypeCondition"/>); rules whose conditions are byte-identical collapse to a single rule
/// (e.g. Barbarian dual-1H hands). Base rules rank above Greater companions so the bonus GA rules are the
/// first to drop under the 25-rule cap. Completed slots (equipped item already meets the goal) are
/// dropped. No LLM involvement.</summary>
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
        // Each slot can contribute up to two rules: an affix-count "base" rule and a Greater-Affix companion.
        // The two tiers are kept separate so base rules rank ABOVE greater rules in the final ruleset — under
        // the cap the bonus GA rules are dropped first, and an item that both adds an affix AND a GA still
        // matches its higher-priority base rule (gold) rather than the GA rule (cyan).
        var baseRules = new List<FilterRule>();
        var greaterRules = new List<FilterRule>();
        foreach (var slot in needy)
        {
            var typeHashes = ResolveTypeHashes(slot.Slot, cls);
            if (typeHashes.Count == 0 && slot.Slot.Slot is GearSlot.Weapon or GearSlot.Offhand or GearSlot.Unknown)
            {
                warnings.Add($"Ambiguous item type for {slot.Slot} — emitting affix-only rule");
            }

            // Highlight any item that improves on what's equipped: require one more of the ranked
            // target affixes than the equipped piece already has (min 1 for an empty/no-gear slot).
            // SlotRuleBuilder clamps the required count to the affix count, so a maxed-out slot yields a
            // rule that matches items with every target affix — i.e. any item equal to the equipped piece.
            var requiredCount = Math.Max(1, slot.MatchedAffixCount + 1);

            var rule = SlotRuleBuilder.BuildMultiType(
                slot.Slot.ToString(), Visibility.Recolor, ColorImprovement, typeHashes,
                slot.TargetAffixIds, greaterAffixIds: [], requiredCount: requiredCount);

            if (rule is null)
            {
                warnings.Add($"No conditions for {slot.Slot}");
                continue;
            }

            baseRules.Add(rule);

            // GA-aware upgrade: an item with the SAME target-affix count as the equipped piece but strictly
            // more Greater Affixes is an upgrade the base rule above misses (it requires one MORE affix). Emit
            // a companion rule only when there is a real equipped item to beat AND it still has room to gain a
            // GA among its matched targets (matchedGreater < matched) — otherwise the rule is unsatisfiable or
            // redundant. See BuildGreaterRule for the chosen rule shape.
            if (slot.EquippedItem is not null && slot.MatchedGreaterAffixCount < slot.MatchedAffixCount)
            {
                var greaterRule = BuildGreaterRule(slot, typeHashes);
                if (greaterRule is not null)
                {
                    greaterRules.Add(greaterRule);
                }
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

    // Builds the Greater-Affix companion rule for a slot: the SAME target-affix count as the equipped piece
    // (min = matched count) plus a global Greater Affix Check (Type 4) requiring one more GA than the equipped
    // piece currently has. The native filter's per-affix "greater" flag (AffixCondition.GreaterEntries) can
    // only force SPECIFIC named affixes to be greater — it cannot express "any (matchedGreater+1) of the
    // targets are greater" — so the count-based Greater Affix Check, D4's own primitive for "at least N
    // greater affixes", is the correct shape (see docs/filter-format.md types 4 and 6). It counts GAs across
    // ALL affixes, not just targets, so it is a deliberate approximation of "more GAs on target affixes".
    private FilterRule? BuildGreaterRule(SlotDiff slot, IReadOnlyList<uint> typeHashes)
    {
        var rule = SlotRuleBuilder.BuildMultiType(
            slot.Slot + " (Greater)", Visibility.Recolor, ColorGreater, typeHashes,
            slot.TargetAffixIds, greaterAffixIds: [], requiredCount: slot.MatchedAffixCount);

        rule?.Conditions.Add(new GreaterAffixCondition(slot.MatchedGreaterAffixCount + 1));
        return rule;
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

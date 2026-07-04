namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Models;

/// <summary>Deterministically turns a <see cref="SlotDiffResult"/> into a native D4 loot filter. Needy
/// slots are grouped into POOLS of genuinely interchangeable slots — the two Ring slots (all classes) and
/// a Barbarian's two 1H weapon hands — then each pool is split by distinct target-affix list, and each
/// resulting group emits exactly ONE rule gated on the pool's shared item-type set and keyed to the WORST
/// equipped member of the group: a PINK rule while any member is not yet maxed on its target affixes
/// (highlights any item holding the SAME OR MORE ranked target affixes than the worst member), or a CYAN
/// rule once every member is maxed (highlights items with at least as many Greater Affixes as the worst
/// member — the only upgrade a static filter can still catch). Non-pooled slots (armor, a lone ring, a
/// lone weapon role) reduce to today's one-rule-per-slot behavior. Plus a Target Uniques rule and a Hide
/// All fallback. Weapon/offhand slots resolve their class-aware role to a SET of item types (one
/// multi-type <see cref="ItemTypeCondition"/>); rules whose conditions are byte-identical collapse to a
/// single rule. Pink rules rank above cyan rules so the lower-value maxed-GA rules are the first to drop
/// under the 25-rule cap. Completed slots (equipped item already meets the goal) are dropped. No LLM
/// involvement.</summary>
/// <param name="nameResolver">The shared fuzzy name resolver.</param>
/// <param name="roleMap">The class-aware weapon role map.</param>
public sealed class ProgressionFilterGenerator(NameResolver nameResolver, WeaponRoleMap roleMap)
{
    private static readonly uint ColorImprovement = FilterColors.LightPurple; // light purple — an affix-count upgrade
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
        // Each slot contributes exactly ONE rule, keyed on IsMaxedOnTargets: a pink "same-or-more target
        // affixes" rule while the equipped piece can still gain a target affix, or a cyan "same-or-more
        // Greater Affixes" rule once it is maxed on target affixes for its rarity. The two tiers are kept
        // in separate lists so pink rules rank ABOVE cyan rules in the final ruleset — under the cap the
        // lower-value maxed-GA rules are dropped first.
        var baseRules = new List<FilterRule>();
        var greaterRules = new List<FilterRule>();

        // 1) Resolve each needy slot's type gate once (preserving order), emitting the ambiguity warning
        // exactly as before, and bucket into ordered pools. Poolable = a genuinely interchangeable slot
        // with a real gate: any Ring, or any weapon/offhand ROLE slot (e.g. a Barbarian's two 1H hands).
        // Everything else (armor slots, roleless/ambiguous weapons, empty gates) is its own singleton pool
        // so nothing merges by accident.
        var pools = new List<(string Key, List<(SlotDiff Slot, IReadOnlyList<uint> Types)> Members)>();
        var poolIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var slot in needy)
        {
            var typeHashes = ResolveTypeHashes(slot.Slot, cls);
            if (typeHashes.Count == 0 && slot.Slot.Slot is GearSlot.Weapon or GearSlot.Offhand or GearSlot.Unknown)
            {
                warnings.Add($"Ambiguous item type for {slot.Slot} — emitting affix-only rule");
            }

            var poolable = typeHashes.Count > 0
                && (slot.Slot.Slot == GearSlot.Ring || slot.Slot.Role != WeaponSlotRole.None);
            var key = poolable
                ? "P:" + string.Join(",", typeHashes)
                : "S:" + slot.Slot + "|" + string.Join(",", typeHashes);

            if (!poolIndex.TryGetValue(key, out var idx))
            {
                idx = pools.Count;
                poolIndex[key] = idx;
                pools.Add((key, []));
            }

            pools[idx].Members.Add((slot, typeHashes));
        }

        // 2) Within each pool, group members by goal-affix-list signature (order-sensitive), preserving
        // first-encounter order. Emit ONE rule per (pool, affix-list) group, keyed to the WORST member —
        // min(MatchedAffixCount) in the pink regime, min(MatchedGreaterAffixCount) in the cyan regime —
        // so a genuine upgrade to the weaker member of an interchangeable pair is never missed. A pool
        // with a single member reduces to exactly today's per-slot behavior.
        foreach (var pool in pools)
        {
            var groups = new List<List<(SlotDiff Slot, IReadOnlyList<uint> Types)>>();
            var groupIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var member in pool.Members)
            {
                var sig = string.Join(",", member.Slot.TargetAffixIds);
                if (!groupIndex.TryGetValue(sig, out var gi))
                {
                    gi = groups.Count;
                    groupIndex[sig] = gi;
                    groups.Add([]);
                }

                groups[gi].Add(member);
            }

            for (var g = 0; g < groups.Count; g++)
            {
                var members = groups[g];
                var typeHashes = members[0].Types;
                var targetAffixIds = members[0].Slot.TargetAffixIds;
                var baseName = PoolLabel(members.Select(m => m.Slot).ToList());
                if (g > 0)
                {
                    baseName = $"{baseName} {g + 1}"; // distinct-goal pools: "Ring", "Ring 2", ...
                }

                FilterRule? rule;
                if (members.Any(m => !m.Slot.IsMaxedOnTargets))
                {
                    // Pink: highlight any item with the SAME OR MORE ranked target affixes than the WORST
                    // equipped member (min 1 for an empty/zero-match slot). SlotRuleBuilder clamps required
                    // to the affix count, so a fully-matched-but-not-capped slot still yields a satisfiable
                    // rule. An empty/unmaxed member counts, so the whole group stays in the pink regime.
                    var requiredCount = Math.Max(1, members.Min(m => m.Slot.MatchedAffixCount));
                    rule = SlotRuleBuilder.BuildMultiType(
                        baseName, Visibility.Recolor, ColorImprovement, typeHashes,
                        targetAffixIds, greaterAffixIds: [], requiredCount: requiredCount);

                    if (rule is null)
                    {
                        warnings.Add($"No conditions for {baseName}");
                        continue;
                    }

                    baseRules.Add(rule);
                }
                else
                {
                    // Cyan: every member maxed on targets — the only catchable upgrade is more Greater
                    // Affixes. Require the same target-affix count plus at least as many GAs as the WORST
                    // member (min 1), so the rule catches an upgrade to either equipped piece.
                    var effectiveCap = members.Min(m => m.Slot.EffectiveTargetCap);
                    rule = SlotRuleBuilder.BuildMultiType(
                        baseName + " (Greater)", Visibility.Recolor, ColorGreater, typeHashes,
                        targetAffixIds, greaterAffixIds: [], requiredCount: effectiveCap);

                    if (rule is null)
                    {
                        warnings.Add($"No conditions for {baseName}");
                        continue;
                    }

                    rule.Conditions.Add(new GreaterAffixCondition(Math.Max(1, members.Min(m => m.Slot.MatchedGreaterAffixCount))));
                    greaterRules.Add(rule);
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

    // Display label for a (pool, affix-list) group. Single-member groups keep today's exact name
    // (SlotKey.ToString()); multi-member pools get a shared label.
    private static string PoolLabel(IReadOnlyList<SlotDiff> members)
    {
        if (members.All(m => m.Slot.Slot == GearSlot.Ring))
        {
            return "Ring";
        }

        if (members.All(m => m.Slot.Role != WeaponSlotRole.None))
        {
            var roles = members.Select(m => m.Slot.Role).Distinct().ToList();
            return roles.Count == 1 ? roles[0].ToString() : "1H Weapon"; // Barb Mainhand+Offhand → "1H Weapon"
        }

        return members[0].Slot.ToString(); // singleton armor/roleless slot: "Helm", "Weapon", "Ring#2", ...
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

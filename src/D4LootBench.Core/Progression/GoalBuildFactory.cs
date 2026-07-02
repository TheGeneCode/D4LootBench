namespace D4LootBench.Core.Progression;

using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;

/// <summary>Converts a parsed build guide into a per-slot <see cref="GoalBuild"/> for the progression
/// diff, resolving affix/unique names to hashes and stamping the caller's chosen threshold onto every
/// slot goal. Mirrors the name-resolution loop of the build-guide filter generator.</summary>
/// <param name="nameResolver">The shared fuzzy name resolver.</param>
public sealed class GoalBuildFactory(NameResolver nameResolver)
{
    /// <summary>Converts a parsed build guide into a per-slot <see cref="GoalBuild"/>, stamping the chosen
    /// threshold onto every slot goal. Talisman/charm slots are skipped (progression is slot-based).</summary>
    /// <param name="guide">The parsed build guide.</param>
    /// <param name="threshold">The "meets goal" threshold to stamp on every slot goal.</param>
    /// <param name="name">An optional display name for the goal build.</param>
    /// <returns>The goal build plus resolution warnings.</returns>
    public GoalBuildResult Create(ParsedBuildGuide guide, MeetsGoalThreshold threshold, string? name = null)
    {
        var goals = new Dictionary<SlotKey, SlotGoal>();
        var warnings = new List<string>();

        foreach (var slot in guide.Slots)
        {
            if (slot.IsTalismanSlot)
            {
                warnings.Add($"Skipped talisman/charm slot \"{slot.SlotLabel}\" — not part of a progression diff.");
                continue;
            }

            if (MapSlot(slot.SlotLabel) is not { } key)
            {
                warnings.Add($"Unrecognized slot \"{slot.SlotLabel}\" — skipped.");
                continue;
            }

            // Resolve affixes in priority order (Priority==0 => positional).
            var affixIds = new List<uint>();
            foreach (var affix in slot.Affixes
                         .OrderBy(a => a.Priority == 0 ? int.MaxValue : a.Priority))
            {
                if (nameResolver.TryResolveAffix(affix.RawName, out var hash, out _))
                {
                    affixIds.Add(hash);
                }
                else
                {
                    warnings.Add($"Could not resolve affix \"{affix.RawName}\" for {slot.SlotLabel}.");
                }
            }

            uint? unique = null;
            if (slot.ItemName is not null &&
                (slot.HasUniqueSentinel || LooksLikeUnique(slot)) &&
                nameResolver.TryResolveUnique(slot.ItemName, out var uHash, out _))
            {
                unique = uHash;
            }
            else if (slot.HasUniqueSentinel && slot.ItemName is not null)
            {
                warnings.Add($"Could not resolve unique \"{slot.ItemName}\" for {slot.SlotLabel}.");
            }

            if (affixIds.Count == 0 && unique is null)
            {
                warnings.Add($"No targets resolved for {slot.SlotLabel} — slot omitted.");
                continue;
            }

            goals[key] = new SlotGoal
            {
                TargetAffixIds = affixIds,
                TargetUnique = unique,
                Threshold = threshold,
            };
        }

        return new GoalBuildResult
        {
            GoalBuild = new GoalBuild(goals, name),
            Warnings = warnings,
        };
    }

    // "ring 1"/"left ring" => Ring#0, "ring 2"/"right ring" => Ring#1, "rings"/"ring" => Ring#0.
    // Generic and concrete weapon/offhand headers both map to the family-default key (ItemType=null); a
    // concrete weapon type (e.g. "Two-Handed Sword") contributes its affixes to the single weapon-family
    // goal (multiple concrete headers collapse last-wins, like duplicate labels).
    private SlotKey? MapSlot(string label)
    {
        switch (label.Trim().ToLowerInvariant())
        {
            case "helm": return new SlotKey(GearSlot.Helm);
            case "chest armor" or "chest": return new SlotKey(GearSlot.ChestArmor);
            case "gloves": return new SlotKey(GearSlot.Gloves);
            case "pants": return new SlotKey(GearSlot.Pants);
            case "boots": return new SlotKey(GearSlot.Boots);
            case "amulet": return new SlotKey(GearSlot.Amulet);
            case "ring 1" or "left ring" or "rings" or "ring": return new SlotKey(GearSlot.Ring, 0);
            case "ring 2" or "right ring": return new SlotKey(GearSlot.Ring, 1);
            case "weapon" or "mainhand" or "main hand": return new SlotKey(GearSlot.Weapon);
            case "offhand" or "off-hand" or "off hand": return new SlotKey(GearSlot.Offhand);
        }

        // Concrete weapon/offhand header (e.g. "Two-Handed Sword", "Focus"): map to the family-default key.
        if (nameResolver.TryResolveItemType(label.Trim(), out var typeHash, out _) &&
            ItemTypeDatabase.ByHash.TryGetValue(typeHash, out var entry) &&
            entry.Category == "Weapons")
        {
            return entry.Name.Contains("Focus") || entry.Name.Contains("Totem") || entry.Name.Contains("Shield")
                ? new SlotKey(GearSlot.Offhand)
                : new SlotKey(GearSlot.Weapon);
        }

        return null;
    }

    // Deliberately permissive: mirror the build-guide generator attempting a unique lookup on any item
    // name. Resolution failure is silent unless the guide asserted a unique via HasUniqueSentinel.
    private static bool LooksLikeUnique(ParsedSlot slot) => slot.ItemName is not null;
}

/// <summary>Result of converting a build guide into a goal build.</summary>
public sealed record GoalBuildResult
{
    /// <summary>Gets the converted goal build.</summary>
    public required GoalBuild GoalBuild { get; init; }

    /// <summary>Gets human-readable warnings (skipped slots, unresolved affixes/uniques).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

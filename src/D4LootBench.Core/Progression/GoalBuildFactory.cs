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
    private static SlotKey? MapSlot(string label) =>
        label.Trim().ToLowerInvariant() switch
        {
            "helm" => new SlotKey(GearSlot.Helm),
            "chest armor" or "chest" => new SlotKey(GearSlot.ChestArmor),
            "gloves" => new SlotKey(GearSlot.Gloves),
            "pants" => new SlotKey(GearSlot.Pants),
            "boots" => new SlotKey(GearSlot.Boots),
            "amulet" => new SlotKey(GearSlot.Amulet),
            "ring 1" or "left ring" or "rings" or "ring" => new SlotKey(GearSlot.Ring, 0),
            "ring 2" or "right ring" => new SlotKey(GearSlot.Ring, 1),
            "weapon" or "mainhand" or "main hand" => new SlotKey(GearSlot.Weapon),
            "offhand" or "off-hand" or "off hand" => new SlotKey(GearSlot.Offhand),
            _ => null,
        };

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

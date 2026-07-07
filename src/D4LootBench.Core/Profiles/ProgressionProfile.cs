using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;

namespace D4LootBench.Core.Profiles;

/// <summary>A saved progression session: the verified gear loadout plus the build/target
/// (raw guide text, format hint, class) needed to regenerate the filter.</summary>
public sealed record ProgressionProfile
{
    /// <summary>Gets the stable identity; also the on-disk filename (<c>{Id:N}.json</c>).</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the user-facing display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>Gets the UTC last-save timestamp (stamped by <see cref="ProfileStore.Save"/>).</summary>
    public DateTimeOffset ModifiedUtc { get; init; }

    /// <summary>Gets the player class the build targets.</summary>
    public PlayerClass PlayerClass { get; init; } = PlayerClass.All;

    /// <summary>Gets the build-guide format hint used when re-importing <see cref="GuideText"/>.</summary>
    public BuildGuideFormat GuideFormat { get; init; } = BuildGuideFormat.Auto;

    /// <summary>Gets the raw pasted build-guide text (the editable build/target).</summary>
    public string GuideText { get; init; } = "";

    /// <summary>Gets the verified gear items (slot, type, rarity, GA flags, affix hashes).</summary>
    public IReadOnlyList<GearItem> Gear { get; init; } = [];

    /// <summary>Gets the override block share code (base64) — static rules that take priority OVER the
    /// generated better-gear rules (e.g. "always show mythics"). <c>null</c>/empty means no override block.</summary>
    public string? OverrideBlockCode { get; init; }

    /// <summary>Gets the overridden-by block share code (base64) — static rules the better-gear rules take
    /// priority over (e.g. "never show rares"), applied below better-gear and above the final catch-all.
    /// <c>null</c>/empty means no overridden-by block.</summary>
    public string? OverriddenByBlockCode { get; init; }
}

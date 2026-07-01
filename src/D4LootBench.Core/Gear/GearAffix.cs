namespace D4LootBench.Core.Gear;

/// <summary>
/// One parsed affix line from a tooltip. <see cref="AffixHash"/> is <c>null</c> when the phrase
/// could not be resolved against the catalog. <see cref="IsGreaterAffix"/> defaults <c>false</c> and
/// is only ever set true in the human review step — greater-affix markers are icons OCR cannot read.
/// </summary>
public sealed record GearAffix
{
    /// <summary>Gets the original OCR line, preserved verbatim for display and re-review.</summary>
    public required string RawText { get; init; }

    /// <summary>Gets catalog display name when resolved; otherwise <c>null</c>.</summary>
    public string? ResolvedName { get; init; }

    /// <summary>Gets resolved affix hash ID, or <c>null</c> when unresolved.</summary>
    public uint? AffixHash { get; init; }

    /// <summary>Gets a value indicating whether this is a greater affix (user-set in review; never inferred from OCR).</summary>
    public bool IsGreaterAffix { get; init; }

    /// <summary>Gets a value indicating whether the affix phrase resolved to a catalog hash.</summary>
    public bool IsResolved => AffixHash is not null;
}

namespace D4LootBench.Core.Gear;

/// <summary>Result of parsing one tooltip: the item plus structural confidence and warnings.</summary>
public sealed record GearParseResult
{
    /// <summary>Gets the parsed item (always present, even for garbage input — with defaults).</summary>
    public required GearItem Item { get; init; }

    /// <summary>Gets structural confidence in the parse.</summary>
    public GearParseConfidence Confidence { get; init; }

    /// <summary>Gets human-readable notes about missing or unresolved data.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

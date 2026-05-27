namespace D4LootBench.Core.Import;

public sealed class ParsedSlot
{
    public required string SlotLabel { get; init; }
    /// <summary>Aspect or unique item name, if present in the guide.</summary>
    public string? ItemName { get; init; }
    /// <summary>True when Maxroll's "Unique Effect" sentinel was encountered — item is a specific unique.</summary>
    public bool HasUniqueSentinel { get; init; }
    /// <summary>True for Seal / Charm N slots — contributes to a show-all talisman rule.</summary>
    public bool IsTalismanSlot { get; init; }
    public List<ParsedAffix> Affixes { get; init; } = [];
}

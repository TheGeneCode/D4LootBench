namespace D4LootBench.Core.Import;

public sealed class ParsedAffix
{
    public required string RawName { get; init; }
    /// <summary>True when the affix was marked with ↑ (Maxroll Greater Affix signal).</summary>
    public bool IsGreaterAffix { get; init; }
    /// <summary>Explicit priority 1–4 (Mobalytics/Icy Veins); 0 means positional order (Maxroll).</summary>
    public int Priority { get; init; }
}

namespace D4LootBench.Core.Import;

public sealed class ParsedBuildGuide
{
    public BuildGuideFormat DetectedFormat { get; init; }
    public List<ParsedSlot> Slots { get; init; } = [];
}

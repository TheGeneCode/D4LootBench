using D4LootBench.Core.Models;

namespace D4LootBench.Ai.Import;

public sealed class BuildGuideImportResult
{
    public required FilterRuleset Ruleset { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

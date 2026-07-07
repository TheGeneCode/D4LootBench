using D4LootBench.Core.Gear;

namespace D4LootBench.App.Tests;

/// <summary>Test <see cref="IGearReader"/> that returns a different line set on each call, so successive
/// reads produce distinguishable items (e.g. verifying index sync or replacing a gear piece).</summary>
internal sealed class SequenceGearReader(params IReadOnlyList<string>[] lineSets) : IGearReader
{
    private int _index;

    public Task<IReadOnlyList<string>> ReadLinesAsync(Stream image, CancellationToken cancellationToken = default)
        => Task.FromResult(lineSets[_index++]);
}

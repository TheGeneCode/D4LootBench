using D4LootBench.Core.Gear;

namespace D4LootBench.App.Tests;

/// <summary>Test <see cref="IGearReader"/> that returns supplied lines (ignoring the image stream), so
/// the wizard can be exercised end-to-end without Windows OCR.</summary>
internal sealed class FakeGearReader(IReadOnlyList<string> lines) : IGearReader
{
    public Task<IReadOnlyList<string>> ReadLinesAsync(Stream image, CancellationToken cancellationToken = default)
        => Task.FromResult(lines);
}

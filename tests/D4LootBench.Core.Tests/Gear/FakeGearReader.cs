using D4LootBench.Core.Gear;

namespace D4LootBench.Core.Tests.Gear;

/// <summary>
/// Test <see cref="IGearReader"/> that returns supplied lines, proving the WinRT seam is fakeable
/// and letting the parser be exercised end-to-end without Windows OCR.
/// </summary>
internal sealed class FakeGearReader(IReadOnlyList<string> lines) : IGearReader
{
    public Task<IReadOnlyList<string>> ReadLinesAsync(Stream image, CancellationToken cancellationToken = default)
        => Task.FromResult(lines);
}

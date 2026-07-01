namespace D4LootBench.Core.Gear;

/// <summary>
/// Runs OCR over a tooltip screenshot and returns the recognized lines in reading order.
/// Isolates the WinRT dependency so the parser is testable headless with saved OCR-text fixtures.
/// </summary>
public interface IGearReader
{
    /// <summary>Recognize the image and return its text lines in reading order.</summary>
    /// <param name="image">Encoded image stream (PNG/JPEG/etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recognized lines, top-to-bottom.</returns>
    Task<IReadOnlyList<string>> ReadLinesAsync(Stream image, CancellationToken cancellationToken = default);
}

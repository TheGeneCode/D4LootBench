using System.Runtime.InteropServices.WindowsRuntime; // AsRandomAccessStream
using D4LootBench.Core.Gear;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace D4LootBench.Vision;

/// <summary>
/// The only production <see cref="IGearReader"/>. Runs in-box <see cref="OcrEngine"/> over a tooltip
/// screenshot. Lives in the one project carrying the windows10 TFM so the WinRT dependency stays
/// isolated from the parser (which is exercised headless with fixtures).
/// </summary>
public sealed class WindowsOcrGearReader : IGearReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ReadLinesAsync(Stream image, CancellationToken cancellationToken = default)
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"))
                     ?? OcrEngine.TryCreateFromUserProfileLanguages()
                     ?? throw new NotSupportedException(
                         "No English OCR language pack is installed. Install the English (US) language feature in Windows Settings.");

        using var ras = image.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(ras);
        using var bitmap = await decoder.GetSoftwareBitmapAsync();

        cancellationToken.ThrowIfCancellationRequested();

        // Fallback: if RecognizeAsync ever rejects the decoded pixel format, convert first with
        // SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied).
        var result = await engine.RecognizeAsync(bitmap);
        return result.Lines.Select(l => l.Text).ToList();
    }
}

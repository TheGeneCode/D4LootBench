using System.Windows.Media;
using D4Loot.Core.Data;

namespace D4Loot.App.Utilities;

internal static class ColorUtility
{
    // Game stores color as ARGB uint32: A=bits 31-24, R=bits 23-16, G=bits 15-8, B=bits 7-0.
    // In-game color picker shows the lower 24 bits as 6-char RGB hex (e.g. "E82222" = red).

    public static Color ArgbToWpf(uint argb)
    {
        // 0 means "game default" — display gold as a visual hint matching the game's native item color
        if (argb == 0) argb = FilterColors.Gold;
        return Color.FromArgb(
            a: (byte)(argb >> 24 & 0xFF),
            r: (byte)(argb >> 16 & 0xFF),
            g: (byte)(argb >> 8 & 0xFF),
            b: (byte)(argb & 0xFF));
    }

    // HSL where H ∈ [0, 360), S/L ∈ [0, 1].
    public static (float H, float S, float L) ArgbToHsl(uint argb)
    {
        var rByte = (byte)(argb >> 16 & 0xFF);
        var gByte = (byte)(argb >> 8 & 0xFF);
        var bByte = (byte)(argb & 0xFF);

        var r = rByte / 255f;
        var g = gByte / 255f;
        var b = bByte / 255f;

        var maxByte = Math.Max(rByte, Math.Max(gByte, bByte));
        var minByte = Math.Min(rByte, Math.Min(gByte, bByte));
        var max     = maxByte / 255f;
        var min     = minByte / 255f;
        var delta   = max - min;
        var l       = (max + min) / 2f;
        var s       = delta == 0f ? 0f : delta / (1f - MathF.Abs(2f * l - 1f));

        var h = 0f;
        if (delta == 0f)
            return (h, s, l);

        if (maxByte == rByte)      h = 60f * ((g - b) / delta % 6f);
        else if (maxByte == gByte) h = 60f * ((b - r) / delta + 2f);
        else                       h = 60f * ((r - g) / delta + 4f);
        if (h < 0f) h += 360f;

        return (h, s, l);
    }

    public static uint HsvToArgb(float h, float s, float v)
    {
        var c = v * s;
        var x = c * (1f - MathF.Abs(h / 60f % 2f - 1f));
        var m = v - c;

        var (r, g, b) = ((int)(h / 60f) % 6) switch
        {
            0 => (c, x, 0f),
            1 => (x, c, 0f),
            2 => (0f, c, x),
            3 => (0f, x, c),
            4 => (x, 0f, c),
            _ => (c, 0f, x)
        };

        return 0xFF000000u | (uint)((r + m) * 255f) << 16
                           | (uint)((g + m) * 255f) << 8
                           | (uint)((b + m) * 255f);
    }

    public static (float H, float S, float V) ArgbToHsv(uint argb)
    {
        var rByte = (byte)(argb >> 16 & 0xFF);
        var gByte = (byte)(argb >> 8 & 0xFF);
        var bByte = (byte)(argb & 0xFF);

        var r = rByte / 255f;
        var g = gByte / 255f;
        var b = bByte / 255f;

        var maxByte = Math.Max(rByte, Math.Max(gByte, bByte));
        var minByte = Math.Min(rByte, Math.Min(gByte, bByte));
        var max     = maxByte / 255f;
        var delta   = max - minByte / 255f;

        if (delta == 0f) return (0f, 0f, max);

        var hue = maxByte == rByte      ? 60f * ((g - b) / delta % 6f)
                : maxByte == gByte      ? 60f * ((b - r) / delta + 2f)
                :                         60f * ((r - g) / delta + 4f);
        if (hue < 0f) hue += 360f;

        return (hue, delta / max, max);
    }

    public static uint HslToArgb(float h, float s, float l)
    {
        var c = (1f - MathF.Abs(2f * l - 1f)) * s;
        var x = c * (1f - MathF.Abs(h / 60f % 2f - 1f));
        var m = l - c / 2f;

        var (r, g, b) = ((int)(h / 60f) % 6) switch
        {
            0 => (c, x, 0f),
            1 => (x, c, 0f),
            2 => (0f, c, x),
            3 => (0f, x, c),
            4 => (x, 0f, c),
            _ => (c, 0f, x)
        };

        var rb = (byte)((r + m) * 255f);
        var gb = (byte)((g + m) * 255f);
        var bb = (byte)((b + m) * 255f);

        // ARGB: A=255, R in bits 23-16, G in bits 15-8, B in bits 7-0
        return 0xFF000000u | (uint)rb << 16 | (uint)gb << 8 | bb;
    }

    /// <summary>
    /// Returns an ARGB color whose hue sits at the midpoint of the largest angular gap
    /// between <paramref name="existingColors"/> hues on the 360° wheel.
    /// Fixed S=0.85 / L=0.55 keeps colours vivid and readable as D4 filter overlays.
    /// </summary>
    public static uint GenerateDistinctColor(IEnumerable<uint> existingColors)
    {
        var hues = existingColors
            .Where(c => c != 0)
            .Select(c => ArgbToHsl(c).H)
            .OrderBy(h => h)
            .ToList();

        if (hues.Count == 0)
            return HslToArgb(200f, 0.85f, 0.55f);

        var bestMid = 0f;
        var bestGap = 0f;

        for (var i = 0; i < hues.Count; i++)
        {
            var next = i + 1 < hues.Count ? hues[i + 1] : hues[0] + 360f;
            var gap  = next - hues[i];
            if (gap <= bestGap) continue;
            bestGap = gap;
            bestMid = (hues[i] + gap / 2f) % 360f;
        }

        return HslToArgb(bestMid, 0.85f, 0.55f);
    }
}

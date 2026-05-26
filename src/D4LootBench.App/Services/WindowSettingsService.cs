using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace D4LootBench.App.Services;

public sealed class WindowSettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "D4LootBench", "window-settings.json");

    private static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public WindowState State         { get; private set; } = WindowState.Normal;
    public double      Width         { get; private set; } = 1100;
    public double      Height        { get; private set; } = 700;
    public double?     Top           { get; private set; }
    public double?     Left          { get; private set; }
    public double      AiPanelHeight { get; set; } = 220;
    public double      RuleListWidth { get; set; } = 320;

    public WindowSettingsService() => Load();

    public void Save(Window window)
    {
        var state  = window.WindowState;
        var bounds = state == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        var stored = new StoredSettings(
            state == WindowState.Minimized ? WindowState.Normal : state,
            bounds.Width,
            bounds.Height,
            bounds.Top,
            bounds.Left,
            AiPanelHeight,
            RuleListWidth);

        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(stored, JsonOptions));
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;

            var stored = JsonSerializer.Deserialize<StoredSettings>(
                File.ReadAllText(SettingsPath), JsonOptions);

            if (stored is null) return;

            State         = stored.State;
            Width         = stored.Width         > 0 ? stored.Width         : Width;
            Height        = stored.Height        > 0 ? stored.Height        : Height;
            Top           = stored.Top;
            Left          = stored.Left;
            AiPanelHeight = stored.AiPanelHeight > 0 ? stored.AiPanelHeight : AiPanelHeight;
            RuleListWidth = stored.RuleListWidth > 0  ? stored.RuleListWidth : RuleListWidth;
        }
        catch { /* corrupt file — use defaults */ }
    }

    private sealed record StoredSettings(
        [property: JsonPropertyName("state")]         WindowState State,
        [property: JsonPropertyName("width")]         double      Width,
        [property: JsonPropertyName("height")]        double      Height,
        [property: JsonPropertyName("top")]           double?     Top,
        [property: JsonPropertyName("left")]          double?     Left,
        [property: JsonPropertyName("aiPanelHeight")] double      AiPanelHeight,
        [property: JsonPropertyName("ruleListWidth")] double      RuleListWidth);
}

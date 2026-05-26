using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using D4LootBench.Ai;

namespace D4LootBench.App.Services;

public sealed class LlmSettingsService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "D4LootBench", "ai-settings.json");

    public LlmSettings Current { get; private set; } = Load();

    public void Save(LlmSettings settings)
    {
        Current = settings;
        Write(settings);
    }

    private static LlmSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var stored = JsonSerializer.Deserialize<StoredSettings>(
                    File.ReadAllText(_path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (stored is not null)
                {
                    var defaults = new LlmSettings();
                    return new LlmSettings
                    {
                        Provider  = stored.Provider,
                        BaseUrl   = string.IsNullOrEmpty(stored.BaseUrl)   ? defaults.BaseUrl   : stored.BaseUrl,
                        ModelName = string.IsNullOrEmpty(stored.ModelName) ? defaults.ModelName : stored.ModelName,
                    };
                }
            }
        }
        catch { /* corrupt file — fall through to defaults */ }
        return new LlmSettings();
    }

    private static void Write(LlmSettings settings)
    {
        var stored = new StoredSettings(settings.Provider, settings.BaseUrl, settings.ModelName);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record StoredSettings(
        [property: JsonPropertyName("provider")]  LlmProviderType Provider,
        [property: JsonPropertyName("baseUrl")]   string BaseUrl,
        [property: JsonPropertyName("modelName")] string ModelName);
}

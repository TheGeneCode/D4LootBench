using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThunderEagle.FilterForge.Ai;

namespace ThunderEagle.FilterForge.App.Services;

public sealed class LlmSettingsService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FilterForge", "ai-settings.json");

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
                var stored = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(_path));
                if (stored is not null)
                    return new LlmSettings
                    {
                        Provider  = stored.Provider,
                        BaseUrl   = stored.BaseUrl,
                        ModelName = stored.ModelName,
                        ApiKey    = DecryptApiKey(stored.ApiKeyProtected),
                    };
            }
        }
        catch { /* corrupt file — fall through to defaults */ }
        return new LlmSettings();
    }

    private static void Write(LlmSettings settings)
    {
        var stored = new StoredSettings(
            settings.Provider,
            settings.BaseUrl,
            settings.ModelName,
            EncryptApiKey(settings.ApiKey));

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string? EncryptApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return null;
        var plainBytes     = Encoding.UTF8.GetBytes(apiKey);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    private static string? DecryptApiKey(string? protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64)) return null;
        try
        {
            var encryptedBytes = Convert.FromBase64String(protectedBase64);
            var plainBytes     = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch { return null; }
    }

    // Serialized form — ApiKeyProtected replaces ApiKey so the plain text never touches disk.
    private sealed record StoredSettings(
        [property: JsonPropertyName("provider")]       LlmProviderType Provider,
        [property: JsonPropertyName("baseUrl")]        string BaseUrl,
        [property: JsonPropertyName("modelName")]      string ModelName,
        [property: JsonPropertyName("apiKeyProtected")] string? ApiKeyProtected);
}

using ThunderEagle.FilterForge.Ai;

namespace ThunderEagle.FilterForge.App.Services;

/// <summary>
/// Singleton ILlmProvider that reads the active settings on every call, so
/// provider changes take effect immediately without restarting the app.
/// </summary>
internal sealed class SettingsAwareLlmProvider(LlmSettingsService settings) : ILlmProvider
{
    public Task<LlmCompletion> GetCompletionAsync(
        string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var inner = LlmProviderFactory.Create(settings.Current);
        return inner.GetCompletionAsync(systemPrompt, userPrompt, ct);
    }
}

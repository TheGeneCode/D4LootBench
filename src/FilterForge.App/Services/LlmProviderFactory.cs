using ThunderEagle.FilterForge.Ai;
using ThunderEagle.FilterForge.Ai.Providers;

namespace ThunderEagle.FilterForge.App.Services;

public static class LlmProviderFactory
{
    public static ILlmProvider Create(LlmSettings settings) => settings.Provider switch
    {
        LlmProviderType.Ollama => new OllamaProvider(settings),
        _                      => new MockLlmProvider()
    };
}

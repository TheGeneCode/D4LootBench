using D4LootBench.Ai;
using D4LootBench.Ai.Providers;

namespace D4LootBench.App.Services;

public static class LlmProviderFactory
{
    public static ILlmProvider Create(LlmSettings settings) => settings.Provider switch
    {
        LlmProviderType.Ollama => new OllamaProvider(settings),
        _                      => new MockLlmProvider()
    };
}

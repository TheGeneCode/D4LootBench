namespace D4LootBench.Ai;

public enum LlmProviderType { Mock, Ollama }

public sealed class LlmSettings
{
    public LlmProviderType Provider { get; set; } = LlmProviderType.Mock;
    public string BaseUrl   { get; set; } = "http://localhost:11434";
    public string ModelName { get; set; } = "qwen2.5-coder:14b";

    /// <summary>Path to a batch prompts file. When set, runs all prompts and exits instead of interactive mode.</summary>
    public string? BatchFile { get; set; }
}

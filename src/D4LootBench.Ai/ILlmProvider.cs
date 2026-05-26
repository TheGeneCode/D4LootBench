namespace D4LootBench.Ai;

/// <summary>
/// Sends a prompt to an LLM backend and returns the raw text completion.
/// Providers are responsible only for the network call; all domain parsing
/// lives in <see cref="RuleAssistant"/>.
/// </summary>
public interface ILlmProvider
{
    Task<LlmCompletion> GetCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default);
}

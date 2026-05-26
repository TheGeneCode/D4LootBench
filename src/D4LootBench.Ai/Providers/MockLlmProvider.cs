namespace D4LootBench.Ai.Providers;

/// <summary>
/// Returns a hardcoded valid intermediate JSON rule instantly.
/// Used during UI development and as "Test Mode" in shipped settings.
/// Zero network calls, zero cost.
/// </summary>
public sealed class MockLlmProvider : ILlmProvider
{
    private const string MockResponse = """
        {
          "name": "Legendary Gloves",
          "visibility": "Show",
          "conditions": [
            { "type": "ItemType", "items": ["Gloves"] },
            { "type": "Rarity",   "rarities": ["Legendary"] }
          ]
        }
        """;

    public Task<LlmCompletion> GetCompletionAsync(
        string systemPrompt, string userPrompt, CancellationToken ct = default)
        => Task.FromResult(LlmCompletion.Ok(MockResponse));
}

using D4LootBench.Core.Models;

namespace D4LootBench.Ai;

public sealed class RuleGenerationResult
{
    public bool        Success      { get; init; }
    public FilterRule? Rule         { get; init; }
    public string?     ErrorMessage { get; init; }

    /// <summary>Candidate names from the catalog when a name the LLM used couldn't be resolved.</summary>
    public IReadOnlyList<string> Suggestions { get; init; } = [];

    /// <summary>Raw JSON string from the LLM — useful for debugging prompt issues.</summary>
    public string? RawResponse { get; init; }

    /// <summary>Non-blocking notices about automatic corrections made during generation (e.g. name truncation).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static RuleGenerationResult Succeeded(FilterRule rule, string rawResponse,
        IReadOnlyList<string>? warnings = null) =>
        new() { Success = true, Rule = rule, RawResponse = rawResponse, Warnings = warnings ?? [] };

    public static RuleGenerationResult Failed(string error, string? rawResponse = null,
        IReadOnlyList<string>? suggestions = null) =>
        new() { Success = false, ErrorMessage = error, RawResponse = rawResponse,
                Suggestions = suggestions ?? [] };
}

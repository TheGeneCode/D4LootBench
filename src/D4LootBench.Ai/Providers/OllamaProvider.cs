using System.Net.Http.Json;
using System.Text.Json;

namespace D4LootBench.Ai.Providers;

/// <summary>
/// Calls an Ollama instance via its OpenAI-compatible /v1/chat/completions endpoint.
/// Uses JSON mode (<c>"format":"json"</c>) to encourage structured output.
/// </summary>
public sealed class OllamaProvider : ILlmProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;

    public OllamaProvider(LlmSettings settings)
    {
        _model = settings.ModelName;
        _http  = new HttpClient { BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/") };
    }

    public async Task<LlmCompletion> GetCompletionAsync(
        string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var body = new
        {
            model    = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt   }
            },
            // JSON Schema format (Ollama 0.4+): grammar-constrained generation enforces
            // the top-level shape so malformed structure is impossible, not just discouraged.
            format = new
            {
                type       = "object",
                required   = new[] { "name", "visibility", "conditions" },
                properties = new
                {
                    name       = new { type = "string" },
                    visibility = new { type = "string", @enum = new[] { "Show", "Recolor", "HideAll" } },
                    conditions = new { type = "array", items = new { type = "object" } }
                }
            },
            stream      = false,
            temperature = 0.1
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("v1/chat/completions", body, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return LlmCompletion.Fail($"Ollama unreachable: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            return LlmCompletion.Fail($"Ollama returned {(int)response.StatusCode}: {detail}");
        }

        string raw;
        try
        {
            raw = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            return LlmCompletion.Fail($"Failed to read response: {ex.Message}");
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            return LlmCompletion.Ok(StripFences(content));
        }
        catch (Exception ex)
        {
            return LlmCompletion.Fail($"Unexpected response shape: {ex.Message}\n{raw}");
        }
    }

    /// <summary>Strips markdown code fences some models add even in JSON mode.</summary>
    private static string StripFences(string content)
    {
        content = content.Trim();
        if (!content.StartsWith("```")) return content;
        var start = content.IndexOf('\n') + 1;
        var end   = content.LastIndexOf("```", StringComparison.Ordinal);
        return start > 0 && end > start ? content[start..end].Trim() : content;
    }

    public void Dispose() => _http.Dispose();
}

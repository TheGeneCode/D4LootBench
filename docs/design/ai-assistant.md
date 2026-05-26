# AI Rule Assistant — Design Document

## Goal

Allow users to describe a filter rule in plain English and have an LLM generate a valid `FilterRule` object. The feature is **entirely opt-in** — the base app (JSON editor + visual editor) works without any AI configuration.

## Distribution Strategy

- **Public release:** AI assistant is present but disabled until the user configures a provider in settings.
- **No hardcoded API key** — the developer never pays for user traffic.
- **Ollama** is the recommended free path; the app ships with a setup guide in the help section.
- Cloud providers (Anthropic, OpenAI) are available for users who already have accounts.

---

## Architecture

### Project: `FilterForge.Ai`

A standalone class library with no WPF dependency. Referenced by `FilterForge.App`.

```
src/FilterForge.Ai/
├── ILlmProvider.cs          # Core abstraction
├── LlmSettings.cs           # Serializable config model
├── RuleAssistant.cs         # Orchestrates prompt + provider + validation
└── Providers/
    ├── OllamaProvider.cs    # HTTP to localhost (OpenAI-compatible endpoint)
    ├── OpenAiProvider.cs    # HTTP to api.openai.com
    └── AnthropicProvider.cs # Anthropic SDK with tool use
```

### Core Interface

```csharp
public interface ILlmProvider
{
    Task<FilterRule> GenerateRuleAsync(string userPrompt, CancellationToken ct = default);
}
```

### Settings Model

```csharp
public sealed class LlmSettings
{
    public LlmProviderType Provider { get; set; } = LlmProviderType.Ollama;
    public string BaseUrl { get; set; } = "http://localhost:11434";  // Ollama default
    public string ModelName { get; set; } = "llama3.2";
    public string? ApiKey { get; set; }  // null for Ollama; encrypted via DPAPI before storage
}

public enum LlmProviderType { Ollama, OpenAi, Anthropic }
```

API keys are encrypted with `System.Security.Cryptography.ProtectedData` (Windows DPAPI) before being written to disk. Never stored in plain text.

---

## Provider Notes

### Ollama
- API is OpenAI-compatible at `{BaseUrl}/v1/chat/completions`
- No auth header needed
- Model list queryable at `{BaseUrl}/api/tags`
- Recommended models: `llama3.2`, `qwen2.5`, `mistral`
- JSON mode: pass `"format": "json"` in request body

### OpenAI
- Same HTTP shape as Ollama, different base URL and `Authorization: Bearer {key}` header
- Use function calling / JSON response format for structured output
- Recommended models: `gpt-4o-mini` (cost), `gpt-4o` (quality)

### Anthropic
- Different API shape; use the official Anthropic .NET SDK
- Use **tool use** (function calling) to force structured `FilterRule` output — most reliable approach
- Recommended model: `claude-haiku-4-5-20251001` (fast + cheap for this task size)

---

## Prompt Engineering

### System Prompt (injected by `RuleAssistant`)

The system prompt includes:
1. The full filter rule JSON schema
2. All known affix hash IDs with display names (from `AffixDatabase`)
3. All item type IDs
4. All skill hash IDs (once complete)
5. Instruction to respond with a single JSON object matching the schema — no prose

### Validation Strategy

LLM output is deserialized into `FilterRule`. If deserialization fails or the result contains unknown IDs, `RuleAssistant` returns a `RuleGenerationResult` with:
- `Success = false`
- `ErrorMessage` — human-readable explanation
- `Suggestions` — list of known affix/item names that partially matched the user's text

The UI presents this as actionable feedback, not a raw error.

---

## UI Integration (FilterForge.App)

- Settings tab: provider dropdown, base URL, model name, API key (masked input)
- "Test Connection" button — sends a trivial prompt and confirms a response
- Chat panel (separate tab or side panel): text input + send button + response area
- Generated rule is previewed before being added to the filter (user confirms or discards)
- Ollama model picker dynamically queries `/api/tags` when Ollama is selected

---

## Build Order

1. **Phase 2 first** — JSON editor and visual editor are prerequisites; the AI assistant only makes sense once users can see and edit rules.
2. **Phase 4A** — `ILlmProvider` + `OllamaProvider` + `RuleAssistant` + settings UI. Validates the full loop with zero key management.
3. **Phase 4B (Polish)** — See below.

---

## Phase 4B — Polish

### Ollama connectivity banner

On panel first-open, fire a background ping to `{BaseUrl}/api/tags`. If it fails, show a non-blocking status banner inside the panel ("Ollama not detected at localhost:11434") with a link to setup docs. No gating — the panel must stay visible so users can find it to configure Ollama in the first place. Dismiss the banner automatically if a subsequent ping succeeds (e.g. after the user starts Ollama and hits Test Connection).

Do **not** hide the panel or block app startup on this check. Startup latency and a non-default port would both make that approach brittle.

### LM Studio support

LM Studio exposes an OpenAI-compatible `/v1/chat/completions` endpoint (default: `http://localhost:1234`). `OllamaProvider` already targets that same endpoint shape, so LM Studio likely works today by just changing the Base URL. The one difference is JSON mode: Ollama uses `"format": "json"` (its own field); LM Studio uses `"response_format": {"type": "json_object"}` (OpenAI-style). Unknown fields are typically ignored, so the Ollama field probably falls on the floor silently and the system prompt carries the weight.

**Action:** test LM Studio manually before writing any code. If JSON output quality is acceptable without the correct mode field, update the help text only ("LM Studio users: set Base URL to `http://localhost:1234`"). If reliability is poor, add a lightweight `LmStudioProvider` that swaps `"format"` → `"response_format"` and introduce an `LmStudio` enum value — no other changes needed.

---

## Developer Learning Notes

This section is for the developer building Phase 4, not end-user docs.

### ML.NET is not relevant here

ML.NET is for training and running your own models (classification, regression, clustering). It has no role in calling external LLMs. Do not reach for it.

### LLM APIs are just HTTP

Calling an LLM API is not meaningfully different from calling any other REST API. The learning surface is:

1. **SDK or `HttpClient`** — NuGet the provider SDK (e.g., `Anthropic`) or call the endpoint directly with `HttpClient`. Ollama and OpenAI share the same HTTP shape.
2. **Message structure** — every LLM call has a `system` prompt (instructions) and a `messages` array (the conversation). That maps directly to what you see in any chat interface.
3. **Tool use / structured output** — instead of getting prose back, you define a JSON schema and the model fills it in. This is how we get a valid `FilterRule` object instead of a paragraph of text. It's the most important technique to learn for this feature.
4. **Error handling** — rate limits, timeouts, malformed responses. Wrap provider calls; never let raw HTTP exceptions surface to the UI.

Prior experience with Claude Code, Copilot, and web chat UIs is directly transferable — the API is exactly those interfaces minus the browser. The concepts (system prompt, user turn, assistant turn) are identical.

---

## Open Questions

- **Skill hash IDs** — need a complete table for all classes before the system prompt can reference skills accurately. Warlock confirmed; Barbarian, Druid, Necromancer, Rogue, Sorcerer pending.
- **Condition coverage** — the assistant should only promise to generate condition types the codec can currently encode. `UnknownCondition` types should be out of scope for AI generation.

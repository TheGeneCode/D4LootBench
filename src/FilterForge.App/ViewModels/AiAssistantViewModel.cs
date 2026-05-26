using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThunderEagle.FilterForge.Ai;
using ThunderEagle.FilterForge.App.Services;
using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels;

public partial class AiAssistantViewModel : ObservableObject
{
    private readonly RuleAssistant _assistant;
    private readonly LlmSettingsService _settingsService;
    private readonly Action<FilterRule> _onAddRule;
    private CancellationTokenSource? _cts;

    public AiAssistantViewModel(
        RuleAssistant assistant,
        LlmSettingsService settingsService,
        Action<FilterRule> onAddRule)
    {
        _assistant       = assistant;
        _settingsService = settingsService;
        _onAddRule       = onAddRule;

        var s = settingsService.Current;
        _provider  = s.Provider;
        _baseUrl   = s.BaseUrl;
        _modelName = s.ModelName;
        _apiKey    = s.ApiKey ?? "";
    }

    // ── Prompt / generation ───────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateRuleCommand))]
    private string _userPrompt = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateRuleCommand))]
    private bool _isGenerating;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingRule))]
    [NotifyPropertyChangedFor(nameof(PendingRuleSummary))]
    [NotifyCanExecuteChangedFor(nameof(AddRuleCommand))]
    [NotifyCanExecuteChangedFor(nameof(DiscardRuleCommand))]
    private FilterRule? _pendingRule;

    public bool HasPendingRule => PendingRule is not null;

    public string PendingRuleSummary => PendingRule is null
        ? ""
        : $"\"{PendingRule.Name}\" — {PendingRule.Conditions.Count} condition(s)";

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateRule(CancellationToken ct)
    {
        _cts?.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        IsGenerating = true;
        PendingRule  = null;
        StatusText   = "";
        HasError     = false;

        try
        {
            var result = await _assistant.GenerateAsync(UserPrompt.Trim(), _cts.Token);

            if (result.Success)
            {
                PendingRule = result.Rule;
                var warnings = result.Warnings.Count > 0
                    ? " Warnings: " + string.Join("; ", result.Warnings)
                    : "";
                StatusText = "Rule generated." + warnings;
                HasError   = false;
            }
            else
            {
                var suggestions = result.Suggestions.Count > 0
                    ? "\nSuggestions: " + string.Join(", ", result.Suggestions.Take(5))
                    : "";
                StatusText = (result.ErrorMessage ?? "Unknown error.") + suggestions;
                HasError   = true;
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled.";
            HasError   = false;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            HasError   = true;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private bool CanGenerate() => !IsGenerating && !string.IsNullOrWhiteSpace(UserPrompt);

    [RelayCommand(CanExecute = nameof(HasPendingRule))]
    private void AddRule()
    {
        if (PendingRule is null) return;
        _onAddRule(PendingRule);
        PendingRule = null;
        StatusText  = "";
        UserPrompt  = "";
    }

    [RelayCommand(CanExecute = nameof(HasPendingRule))]
    private void DiscardRule()
    {
        PendingRule = null;
        StatusText  = "";
    }

    // ── Provider settings ─────────────────────────────────────────────────

    public static IReadOnlyList<LlmProviderType> Providers { get; } =
        Enum.GetValues<LlmProviderType>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfigurableProvider))]
    [NotifyPropertyChangedFor(nameof(IsOllamaProvider))]
    [NotifyPropertyChangedFor(nameof(IsApiKeyVisible))]
    private LlmProviderType _provider;

    [ObservableProperty] private string _baseUrl;
    [ObservableProperty] private string _modelName;
    [ObservableProperty] private string _apiKey;

    /// <summary>True for any provider that has model settings (i.e. not Mock).</summary>
    public bool IsConfigurableProvider => Provider != LlmProviderType.Mock;
    /// <summary>True only for Ollama — the only provider where BaseUrl is configurable.</summary>
    public bool IsOllamaProvider => Provider == LlmProviderType.Ollama;
    public bool IsApiKeyVisible  => Provider is LlmProviderType.OpenAi or LlmProviderType.Anthropic;

    partial void OnProviderChanged(LlmProviderType value)
    {
        // Populate static fallbacks immediately so the dropdown isn't empty
        AvailableModels.Clear();
        var defaults = value switch
        {
            LlmProviderType.Anthropic => _anthropicFallbacks,
            LlmProviderType.OpenAi    => _openAiFallbacks,
            _                          => []
        };
        foreach (var m in defaults) AvailableModels.Add(m);

        // Ollama has no auth requirement — auto-fetch on switch
        if (value == LlmProviderType.Ollama)
            _ = RefreshModels(CancellationToken.None);
    }

    // ── Model list ────────────────────────────────────────────────────────

    private static readonly string[] _anthropicFallbacks =
    [
        "claude-haiku-4-5-20251001",
        "claude-sonnet-4-6",
        "claude-opus-4-7",
    ];

    private static readonly string[] _openAiFallbacks =
    [
        "gpt-4o-mini",
        "gpt-4o",
        "gpt-4.1",
        "o1-mini",
        "o1",
    ];

    public ObservableCollection<string> AvailableModels { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshModelsCommand))]
    private bool _isRefreshingModels;

    [RelayCommand(CanExecute = nameof(CanRefreshModels))]
    private async Task RefreshModels(CancellationToken ct)
    {
        IsRefreshingModels = true;
        try
        {
            var models = Provider switch
            {
                LlmProviderType.Ollama    => await FetchOllamaModels(ct),
                LlmProviderType.Anthropic => await FetchAnthropicModels(ct),
                LlmProviderType.OpenAi    => await FetchOpenAiModels(ct),
                _                          => []
            };
            AvailableModels.Clear();
            foreach (var m in models) AvailableModels.Add(m);
        }
        catch { /* unreachable or auth failure — static fallbacks remain usable */ }
        finally
        {
            IsRefreshingModels = false;
        }
    }

    private bool CanRefreshModels() => !IsRefreshingModels;

    private async Task<IEnumerable<string>> FetchOllamaModels(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var response = await http.GetFromJsonAsync<OllamaTagsResponse>(
            $"{BaseUrl.TrimEnd('/')}/api/tags", ct);
        return response?.Models?.Select(m => m.Name) ?? [];
    }

    private async Task<IEnumerable<string>> FetchAnthropicModels(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Add("x-api-key", ApiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        var response = await http.GetFromJsonAsync<AnthropicModelsResponse>(
            "https://api.anthropic.com/v1/models", ct);
        return response?.Data?.Select(m => m.Id) ?? [];
    }

    private async Task<IEnumerable<string>> FetchOpenAiModels(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
        var response = await http.GetFromJsonAsync<OpenAiModelsResponse>(
            "https://api.openai.com/v1/models", ct);
        // Filter to chat-completion models only; exclude embeddings, audio, image, etc.
        return response?.Data?
            .Select(m => m.Id)
            .Where(id => id.StartsWith("gpt-") || id.StartsWith("o1") ||
                         id.StartsWith("o3") || id.StartsWith("o4"))
            .Order()
            ?? Enumerable.Empty<string>();
    }

    private sealed record OllamaTagsResponse(
        [property: JsonPropertyName("models")] List<OllamaModelEntry>? Models);
    private sealed record OllamaModelEntry(
        [property: JsonPropertyName("name")] string Name);

    private sealed record AnthropicModelsResponse(
        [property: JsonPropertyName("data")] List<AnthropicModelEntry>? Data);
    private sealed record AnthropicModelEntry(
        [property: JsonPropertyName("id")] string Id);

    private sealed record OpenAiModelsResponse(
        [property: JsonPropertyName("data")] List<OpenAiModelEntry>? Data);
    private sealed record OpenAiModelEntry(
        [property: JsonPropertyName("id")] string Id);

    // ── Test connection ───────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private bool _isTesting;

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.Save(new LlmSettings
        {
            Provider  = Provider,
            BaseUrl   = BaseUrl,
            ModelName = ModelName,
            ApiKey    = string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey,
        });
        StatusText = "Settings saved.";
        HasError   = false;
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestConnection(CancellationToken ct)
    {
        IsTesting  = true;
        StatusText = "Testing connection…";
        HasError   = false;

        var tempSettings = new LlmSettings
        {
            Provider  = Provider,
            BaseUrl   = BaseUrl,
            ModelName = ModelName,
            ApiKey    = string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey,
        };

        try
        {
            var provider   = LlmProviderFactory.Create(tempSettings);
            var completion = await provider.GetCompletionAsync(
                "Respond with: {\"ok\":true}", "ping", ct);

            StatusText = completion.IsSuccess ? "Connection OK." : $"Failed: {completion.Error}";
            HasError   = !completion.IsSuccess;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Test cancelled.";
            HasError   = false;
        }
        catch (Exception ex)
        {
            StatusText = $"Connection failed: {ex.Message}";
            HasError   = true;
        }
        finally
        {
            IsTesting = false;
        }
    }

    private bool CanTest() => !IsTesting;
}

using System.Text.Json;
using D4LootBench.Ai;
using D4LootBench.Ai.Providers;
using D4LootBench.Core.Data;
using D4LootBench.Core.Serialization;
using D4LootBench.Core.Validation;

// ── Bootstrap ───────────────────────────────────────────────────────────────
var dataService = new FilterDataService();
FilterDataContext.Set(dataService);

var settings = ParseArgs(args);
var provider = CreateProvider(settings);
var promptBuilder = new SystemPromptBuilder(dataService);
var resolver      = new NameResolver(dataService);
var validator     = new FilterValidator();
var assistant     = new RuleAssistant(provider, promptBuilder, resolver, validator);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"D4LootBench PromptLab  |  provider={settings.Provider}  model={settings.ModelName}");
Console.ResetColor();
Console.WriteLine();

if (settings.BatchFile is not null)
{
    await RunBatchAsync(settings.BatchFile, assistant);
}
else
{
    await RunInteractiveAsync(assistant);
}

// ── Interactive mode ─────────────────────────────────────────────────────────
async Task RunInteractiveAsync(RuleAssistant a)
{
    Console.WriteLine("Type a prompt and press Enter. Empty line to quit.");
    Console.WriteLine();

    while (true)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("Prompt> ");
        Console.ResetColor();

        var prompt = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(prompt)) break;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Generating...");
        Console.ResetColor();

        var result = await a.GenerateAsync(prompt);
        PrintResult(result, verbose: true);
        Console.WriteLine();
    }
}

// ── Batch mode ───────────────────────────────────────────────────────────────
async Task RunBatchAsync(string filePath, RuleAssistant a)
{
    if (!File.Exists(filePath))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Batch file not found: {filePath}");
        Console.ResetColor();
        return;
    }

    var prompts = File.ReadAllLines(filePath)
        .Select(l => l.Trim())
        .Where(l => l.Length > 0 && !l.StartsWith('#'))
        .ToList();

    Console.WriteLine($"Running {prompts.Count} prompts from {Path.GetFileName(filePath)}...");
    Console.WriteLine(new string('─', 60));

    var passed = 0;
    var failed = 0;

    for (var i = 0; i < prompts.Count; i++)
    {
        var prompt = prompts[i];
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n[{i + 1}/{prompts.Count}] {prompt}");
        Console.ResetColor();

        var result = await a.GenerateAsync(prompt);
        PrintResult(result, verbose: false);

        if (result.Success) passed++; else failed++;
    }

    Console.WriteLine();
    Console.WriteLine(new string('─', 60));
    Console.ForegroundColor = passed == prompts.Count ? ConsoleColor.Green : ConsoleColor.Yellow;
    Console.WriteLine($"Results: {passed} passed, {failed} failed out of {prompts.Count}");
    Console.ResetColor();
}

// ── Shared result printer ────────────────────────────────────────────────────
void PrintResult(RuleGenerationResult result, bool verbose)
{
    if (verbose && result.RawResponse is not null)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n--- Raw LLM Response ---");
        Console.WriteLine(result.RawResponse);
        Console.ResetColor();
    }

    if (result.Success)
    {
        foreach (var w in result.Warnings)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ! {w}");
            Console.ResetColor();
        }

        var rule       = result.Rule!;
        var condTypes  = string.Join(", ", rule.Conditions.Select(c => c.GetType().Name.Replace("Condition", "")));

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ✓ ");
        Console.ResetColor();
        Console.Write($"\"{rule.Name}\"");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  [{condTypes}]");
        Console.ResetColor();

        if (verbose)
        {
            var json = JsonSerializer.Serialize(rule, FilterJsonOptions.Default);
            Console.WriteLine("\n--- Resolved FilterRule ---");
            Console.WriteLine(json);
        }
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ✗ {result.ErrorMessage}");
        Console.ResetColor();

        if (result.Suggestions.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"    Suggestions: {string.Join(", ", result.Suggestions)}");
            Console.ResetColor();
        }
    }
}

// ── Helpers ──────────────────────────────────────────────────────────────────
static LlmSettings ParseArgs(string[] args)
{
    var settings = new LlmSettings();
    for (var i = 0; i < args.Length - 1; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--provider":
                if (Enum.TryParse<LlmProviderType>(args[i + 1], ignoreCase: true, out var p))
                    settings.Provider = p;
                break;
            case "--model":
                settings.ModelName = args[i + 1];
                break;
            case "--url":
                settings.BaseUrl = args[i + 1];
                break;
            case "--batch":
                settings.BatchFile = args[i + 1];
                break;
        }
    }
    return settings;
}

static ILlmProvider CreateProvider(LlmSettings s) => s.Provider switch
{
    LlmProviderType.Ollama => new OllamaProvider(s),
    _                      => new MockLlmProvider()
};

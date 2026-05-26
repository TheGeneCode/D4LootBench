namespace D4LootBench.Ai;

public readonly struct LlmCompletion
{
    public string? Content { get; init; }
    public string? Error   { get; init; }
    public bool IsSuccess  => Content is not null;

    public static LlmCompletion Ok(string content)  => new() { Content = content };
    public static LlmCompletion Fail(string error)   => new() { Error = error };
}

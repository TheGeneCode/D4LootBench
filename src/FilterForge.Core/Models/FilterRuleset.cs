using ThunderEagle.FilterForge.Core.Validation;

namespace ThunderEagle.FilterForge.Core.Models;

public sealed class FilterRuleset
{
    /// <summary>Game-enforced maximum of 25 rules per filter.</summary>
    public const int MaxRuleCount = 25;

    public FilterRuleset() { }

    public FilterRuleset(string name, IEnumerable<FilterRule> rules)
    {
        Name  = name;
        Rules = rules.ToList();
    }

    public string          Name         { get; set; } = "Unnamed Filter";
    public List<FilterRule> Rules        { get; set; } = [];
    public string?         OriginalCode { get; set; }

    /// <summary>Validates against game-enforced constraints. Returns the error messages
    /// of the resulting <see cref="ValidationResult"/> as strings, preserving the legacy
    /// callsite shape. New callers should use <see cref="IFilterValidator"/> directly.</summary>
    public List<string> Validate() =>
        new FilterValidator().Validate(this).Errors.Select(i => i.Message).ToList();
}

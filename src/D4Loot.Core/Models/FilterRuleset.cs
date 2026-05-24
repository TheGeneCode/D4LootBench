namespace D4Loot.Core.Models;

public sealed class FilterRuleset
{
    public FilterRuleset() { }

    public FilterRuleset(string name, IEnumerable<FilterRule> rules)
    {
        Name  = name;
        Rules = rules.ToList();
    }

    public string          Name         { get; set; } = "Unnamed Filter";
    public List<FilterRule> Rules        { get; set; } = [];
    public string?         OriginalCode { get; set; }
}

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

    /// <summary>Validates all rules against game-enforced constraints.
    /// Returns an empty list if valid, or a list of error messages.</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (Rules.Count > 25)
            errors.Add($"Filter has {Rules.Count} rules — maximum is 25.");

        foreach (var (rule, i) in Rules.Select((r, i) => (r, i)))
        {
            var prefix = $"Rule {i + 1} (\"{rule.Name}\")";

            if (rule.Name.Length > 24)
                errors.Add($"{prefix}: name is {rule.Name.Length} characters — maximum is 24.");

            foreach (var cond in rule.Conditions)
            {
                switch (cond)
                {
                    case ItemPowerCondition ip:
                        if (ip.Maximum > 900)
                            errors.Add($"{prefix}: item power maximum is {ip.Maximum} — game cap is 900.");
                        break;

                    case GreaterAffixCondition ga:
                        if (ga.MinimumCount < 1 || ga.MinimumCount > 4)
                            errors.Add($"{prefix}: greater affix minimum count is {ga.MinimumCount} — game allows 1–4.");
                        break;
                }
            }
        }

        return errors;
    }
}

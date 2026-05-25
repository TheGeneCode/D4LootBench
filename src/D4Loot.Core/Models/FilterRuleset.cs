namespace D4Loot.Core.Models;

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

    /// <summary>Validates all rules against game-enforced constraints.
    /// Returns an empty list if valid, or a list of error messages.</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (Rules.Count > MaxRuleCount)
            errors.Add($"Filter has {Rules.Count} rules — maximum is {MaxRuleCount}.");

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

                    case AffixCondition a:
                        if (a.AffixIds.Count > AffixCondition.MaxSelectionCount)
                            errors.Add($"{prefix}: required affixes has {a.AffixIds.Count} affixes — maximum is {AffixCondition.MaxSelectionCount}.");
                        break;

                    case OptionalAffixCondition oa:
                        if (oa.AffixIds.Count > OptionalAffixCondition.MaxSelectionCount)
                            errors.Add($"{prefix}: optional affixes has {oa.AffixIds.Count} affixes — maximum is {OptionalAffixCondition.MaxSelectionCount}.");
                        break;

                    case SpecificUniqueCondition su:
                        if (su.UniqueIds.Count > SpecificUniqueCondition.MaxSelectionCount)
                            errors.Add($"{prefix}: specific uniques has {su.UniqueIds.Count} items — maximum is {SpecificUniqueCondition.MaxSelectionCount}.");
                        break;

                    case TalismanSetCondition ts:
                        if (ts.SetIds.Count > TalismanSetCondition.MaxSelectionCount)
                            errors.Add($"{prefix}: talisman sets has {ts.SetIds.Count} sets — maximum is {TalismanSetCondition.MaxSelectionCount}.");
                        break;
                }
            }
        }

        return errors;
    }
}

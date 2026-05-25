using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.Core.Validation;

public sealed class FilterValidator : IFilterValidator
{
    public const int MaxRuleNameLength = 24;
    public const int ItemPowerCap      = 900;
    public const int GreaterAffixMinCountFloor = 1;
    public const int GreaterAffixMinCountCeiling = 4;

    public ValidationResult Validate(FilterRuleset ruleset)
    {
        var issues = new List<ValidationIssue>();

        if (ruleset.Rules.Count > FilterRuleset.MaxRuleCount)
            issues.Add(new(ValidationSeverity.Error,
                $"Filter has {ruleset.Rules.Count} rules — maximum is {FilterRuleset.MaxRuleCount}."));

        for (var i = 0; i < ruleset.Rules.Count; i++)
            ValidateRule(ruleset.Rules[i], i, issues);

        return new ValidationResult(issues);
    }

    private static void ValidateRule(FilterRule rule, int index, List<ValidationIssue> issues)
    {
        var prefix = $"Rule {index + 1} (\"{rule.Name}\")";

        if (rule.Name.Length > MaxRuleNameLength)
            issues.Add(new(ValidationSeverity.Error,
                $"{prefix}: name is {rule.Name.Length} characters — maximum is {MaxRuleNameLength}.", index));

        foreach (var cond in rule.Conditions)
        {
            switch (cond)
            {
                case ItemPowerCondition ip when ip.Maximum > ItemPowerCap:
                    issues.Add(new(ValidationSeverity.Error,
                        $"{prefix}: item power maximum is {ip.Maximum} — game cap is {ItemPowerCap}.", index));
                    break;

                case GreaterAffixCondition ga when ga.MinimumCount < GreaterAffixMinCountFloor
                                                || ga.MinimumCount > GreaterAffixMinCountCeiling:
                    issues.Add(new(ValidationSeverity.Error,
                        $"{prefix}: greater affix minimum count is {ga.MinimumCount} — game allows {GreaterAffixMinCountFloor}–{GreaterAffixMinCountCeiling}.", index));
                    break;

                case AffixCondition a when a.AffixIds.Count > AffixCondition.MaxSelectionCount:
                    issues.Add(new(ValidationSeverity.Error,
                        $"{prefix}: required affixes has {a.AffixIds.Count} affixes — maximum is {AffixCondition.MaxSelectionCount}.", index));
                    break;

                case OptionalAffixCondition oa when oa.AffixIds.Count > OptionalAffixCondition.MaxSelectionCount:
                    issues.Add(new(ValidationSeverity.Error,
                        $"{prefix}: optional affixes has {oa.AffixIds.Count} affixes — maximum is {OptionalAffixCondition.MaxSelectionCount}.", index));
                    break;

                case SpecificUniqueCondition su when su.UniqueIds.Count > SpecificUniqueCondition.MaxSelectionCount:
                    issues.Add(new(ValidationSeverity.Error,
                        $"{prefix}: specific uniques has {su.UniqueIds.Count} items — maximum is {SpecificUniqueCondition.MaxSelectionCount}.", index));
                    break;

                case TalismanSetCondition ts when ts.SetIds.Count > TalismanSetCondition.MaxSelectionCount:
                    issues.Add(new(ValidationSeverity.Error,
                        $"{prefix}: talisman sets has {ts.SetIds.Count} sets — maximum is {TalismanSetCondition.MaxSelectionCount}.", index));
                    break;
            }
        }
    }
}

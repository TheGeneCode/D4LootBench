using D4LootBench.Core.Data;
using D4LootBench.Core.Models;
using D4LootBench.Core.Validation;
using Shouldly;

namespace D4LootBench.Core.Tests.Validation;

public class FilterValidatorTests
{
    private static readonly IFilterValidator Validator = new FilterValidator();

    private static FilterRule SimpleRule(string name = "R", Visibility v = Visibility.Show) =>
        new(name, v, FilterColors.Blue, [], true);

    [Fact]
    public void EmptyFilter_IsValid()
    {
        var result = Validator.Validate(new FilterRuleset("Empty", []));
        result.IsValid.ShouldBeTrue();
        result.HasIssues.ShouldBeFalse();
    }

    [Fact]
    public void RuleCountAtLimit_IsValid()
    {
        var rules = Enumerable.Range(0, FilterRuleset.MaxRuleCount).Select(i => SimpleRule($"R{i}"));
        Validator.Validate(new FilterRuleset("F", rules)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void RuleCountOverLimit_Errors_AtFilterLevel()
    {
        var rules = Enumerable.Range(0, FilterRuleset.MaxRuleCount + 1).Select(i => SimpleRule($"R{i}"));
        var result = Validator.Validate(new FilterRuleset("F", rules));
        result.IsValid.ShouldBeFalse();
        var rc = result.Errors.Single(e => e.RuleIndex is null);
        rc.Message.ShouldContain("maximum is 25");
    }

    [Fact]
    public void RuleName_AtBoundary_IsValid()
    {
        // 24 chars is the max
        Validator.Validate(new FilterRuleset("F", [SimpleRule(new string('x', 24))]))
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void RuleName_OverBoundary_Errors_WithRuleIndex()
    {
        var result = Validator.Validate(new FilterRuleset("F", [SimpleRule(new string('x', 25))]));
        result.IsValid.ShouldBeFalse();
        var issue = result.Errors.Single();
        issue.RuleIndex.ShouldBe(0);
        issue.Message.ShouldContain("maximum is 24");
    }

    [Fact]
    public void ItemPowerMaximum_AtCap_IsValid()
    {
        var rule = new FilterRule("R", Visibility.Show, FilterColors.Blue,
            [new ItemPowerCondition(0, 900)], true);
        Validator.Validate(new FilterRuleset("F", [rule])).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ItemPowerMaximum_OverCap_Errors()
    {
        var rule = new FilterRule("R", Visibility.Show, FilterColors.Blue,
            [new ItemPowerCondition(0, 901)], true);
        Validator.Validate(new FilterRuleset("F", [rule])).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1)] [InlineData(4)]
    public void GreaterAffixMinimumCount_InRange_IsValid(int count)
    {
        var rule = new FilterRule("R", Visibility.Show, FilterColors.Blue,
            [new GreaterAffixCondition(count)], true);
        Validator.Validate(new FilterRuleset("F", [rule])).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)] [InlineData(5)] [InlineData(-1)]
    public void GreaterAffixMinimumCount_OutOfRange_Errors(int count)
    {
        var rule = new FilterRule("R", Visibility.Show, FilterColors.Blue,
            [new GreaterAffixCondition(count)], true);
        Validator.Validate(new FilterRuleset("F", [rule])).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AffixCondition_AtLimit_IsValid()
    {
        var ids = Enumerable.Range(0, AffixCondition.MaxSelectionCount).Select(i => (uint)i).ToList();
        var rule = new FilterRule("R", Visibility.Show, FilterColors.Blue,
            [new AffixCondition(ids, 1)], true);
        Validator.Validate(new FilterRuleset("F", [rule])).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AffixCondition_OverLimit_Errors_WithRuleIndex()
    {
        var ids = Enumerable.Range(0, AffixCondition.MaxSelectionCount + 1).Select(i => (uint)i).ToList();
        var rule = new FilterRule("R", Visibility.Show, FilterColors.Blue,
            [new AffixCondition(ids, 1)], true);
        var result = Validator.Validate(new FilterRuleset("F", [rule]));
        result.IsValid.ShouldBeFalse();
        result.Errors.Single().RuleIndex.ShouldBe(0);
    }

    [Fact]
    public void OptionalAffixCondition_OverLimit_Errors()
    {
        var ids = Enumerable.Range(0, OptionalAffixCondition.MaxSelectionCount + 1).Select(i => (uint)i).ToList();
        var rule = new FilterRule("R", Visibility.Show, FilterColors.Blue,
            [new OptionalAffixCondition(ids, 1)], true);
        Validator.Validate(new FilterRuleset("F", [rule])).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void SpecificUnique_OverLimit_Errors()
    {
        var ids = Enumerable.Range(0, SpecificUniqueCondition.MaxSelectionCount + 1).Select(i => (uint)i).ToList();
        var rule = new FilterRule("R", Visibility.Show, FilterColors.Blue,
            [new SpecificUniqueCondition(ids)], true);
        Validator.Validate(new FilterRuleset("F", [rule])).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void TalismanSet_OverLimit_Errors()
    {
        var ids = Enumerable.Range(0, TalismanSetCondition.MaxSelectionCount + 1).Select(i => (uint)i).ToList();
        var rule = new FilterRule("R", Visibility.Show, FilterColors.Blue,
            [new TalismanSetCondition { SetIds = ids }], true);
        Validator.Validate(new FilterRuleset("F", [rule])).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void MultipleIssues_AllReported_WithCorrectIndices()
    {
        var rules = new List<FilterRule>
        {
            SimpleRule(new string('x', 25)),                                                       // rule 0: name too long
            new("ok", Visibility.Show, FilterColors.Blue, [new ItemPowerCondition(0, 999)], true), // rule 1: ip max
            new("ok2", Visibility.Show, FilterColors.Blue, [new GreaterAffixCondition(7)], true),  // rule 2: ga count
        };
        var result = Validator.Validate(new FilterRuleset("F", rules));
        result.Errors.Count().ShouldBe(3);
        result.Errors.Select(e => e.RuleIndex).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void FilterRuleset_Validate_LegacyApi_DelegatesAndReturnsErrorMessages()
    {
        var ruleset = new FilterRuleset("F", [SimpleRule(new string('x', 25))]);
        var errors  = ruleset.Validate();
        errors.Count.ShouldBe(1);
        errors[0].ShouldContain("maximum is 24");
    }
}

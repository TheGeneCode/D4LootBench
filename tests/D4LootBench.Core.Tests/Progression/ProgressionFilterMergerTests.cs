using D4LootBench.Core.Codec;
using D4LootBench.Core.Models;
using D4LootBench.Core.Progression;
using Shouldly;

namespace D4LootBench.Core.Tests.Progression;

public sealed class ProgressionFilterMergerTests
{
    private static readonly ProgressionFilterMerger Merger = new();

    private static FilterRule Rule(string name, Visibility visibility = Visibility.Recolor) =>
        new(name, visibility, FilterRule.PackColor(10, 20, 30), []);

    private static FilterRule HideAll(string name = "Hide All") =>
        new(name, Visibility.HideAll, 0, []);

    /// <summary>Better-gear generator output shape: [Target Uniques, …slots, Hide All].</summary>
    private static FilterRuleset BetterGear(params string[] bodyNames)
    {
        var rules = bodyNames.Select(n => Rule(n)).ToList();
        rules.Add(HideAll());
        return new FilterRuleset("BG", rules);
    }

    [Fact]
    public void Merge_empty_blocks_equals_better_gear_body_plus_single_hideall()
    {
        var betterGear = BetterGear("Uniques", "SlotA", "SlotB");

        var result = Merger.Merge([], betterGear, []);

        result.Ruleset.Rules.Select(r => r.Name).ShouldBe(["Uniques", "SlotA", "SlotB", "Hide All"]);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.HideAll).ShouldBe(1);
        result.Ruleset.Rules.Count.ShouldBe(betterGear.Rules.Count);
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_orders_override_first_then_better_then_overriddenby_then_hideall()
    {
        var betterGear = BetterGear("U", "S");

        var result = Merger.Merge([Rule("O1")], betterGear, [Rule("X1")]);

        result.Ruleset.Rules.Select(r => r.Name).ShouldBe(["O1", "U", "S", "X1", "Hide All"]);
        result.Ruleset.Rules[^1].Visibility.ShouldBe(Visibility.HideAll);
    }

    [Fact]
    public void Merge_strips_better_gear_trailing_hideall()
    {
        var betterGear = BetterGear("U", "S");

        var result = Merger.Merge([], betterGear, []);

        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.HideAll).ShouldBe(1);
        result.Ruleset.Rules[^1].Visibility.ShouldBe(Visibility.HideAll);
    }

    [Fact]
    public void Merge_drops_hideall_from_override_block_with_warning()
    {
        var betterGear = BetterGear("U");

        var result = Merger.Merge([Rule("O1"), HideAll("Override Hide")], betterGear, []);

        var names = result.Ruleset.Rules.Select(r => r.Name).ToList();
        names.ShouldContain("O1");
        names.ShouldNotContain("Override Hide");
        result.Warnings.Count.ShouldBe(1);
        result.Warnings[0].ShouldContain("override block");
    }

    [Fact]
    public void Merge_uses_overriddenby_trailing_hideall_as_final_catchall()
    {
        var betterGear = BetterGear("U", "S");

        var result = Merger.Merge([Rule("O1")], betterGear, [Rule("X1"), HideAll("Hide Junk")]);

        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.HideAll).ShouldBe(1);
        result.Ruleset.Rules[^1].Name.ShouldBe("Hide Junk");
        // override(1) + body(2) + overriddenBy(2, incl. its own Hide All) = 5
        result.Ruleset.Rules.Count.ShouldBe(5);
    }

    [Fact]
    public void Merge_trims_better_gear_from_tail_when_over_cap()
    {
        var bodyNames = Enumerable.Range(1, 30).Select(i => $"BG{i}").ToArray();
        var betterGear = BetterGear(bodyNames);

        var result = Merger.Merge([], betterGear, []);

        result.Ruleset.Rules.Count.ShouldBe(FilterRuleset.MaxRuleCount);
        result.Ruleset.Rules.Count(r => r.Visibility != Visibility.HideAll).ShouldBe(24);
        result.Ruleset.Rules[^1].Visibility.ShouldBe(Visibility.HideAll);
        var droppedWarnings = result.Warnings.Where(w => w.Contains("dropped better-gear rule")).ToList();
        droppedWarnings.Count.ShouldBe(6);
        droppedWarnings.ShouldContain(w => w.Contains("BG25"));
        droppedWarnings.ShouldContain(w => w.Contains("BG30"));
        result.OverBudget.ShouldBeFalse();
    }

    [Fact]
    public void Merge_sets_OverBudget_when_static_blocks_exceed_cap()
    {
        var overrideBlock = Enumerable.Range(1, 26).Select(i => Rule($"O{i}")).ToList();
        var betterGear = BetterGear("U", "S");

        var result = Merger.Merge(overrideBlock, betterGear, []);

        result.Ruleset.Rules.Count(r => r.Name.StartsWith('U') || r.Name.StartsWith('S')).ShouldBe(0);
        result.OverBudget.ShouldBeTrue();
        result.Warnings.ShouldContain(w => w.Contains("exceeding the") && w.Contains("rule limit"));
    }

    [Fact]
    public void Merged_ruleset_round_trips_through_codec()
    {
        var betterGear = BetterGear("U", "S", "T");
        var result = Merger.Merge([Rule("O1")], betterGear, [Rule("X1"), HideAll("Hide Junk")]);

        var decoded = FilterCodec.Decode(FilterCodec.Encode(result.Ruleset));

        decoded.Rules.Count.ShouldBe(result.Ruleset.Rules.Count);
        decoded.Rules.Select(r => r.Name).ShouldBe(result.Ruleset.Rules.Select(r => r.Name));
    }

    [Fact]
    public void Merge_better_gear_with_no_trailing_hideall_keeps_entire_body()
    {
        var betterGear = new FilterRuleset("BG", [Rule("U"), Rule("S")]);

        var result = Merger.Merge([], betterGear, []);

        result.Ruleset.Rules.Select(r => r.Name).ShouldBe(["U", "S", "Hide All"]);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.HideAll).ShouldBe(1);
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_better_gear_with_zero_rules_produces_only_catchall()
    {
        var betterGear = new FilterRuleset("BG", []);

        var result = Merger.Merge([], betterGear, []);

        result.Ruleset.Rules.Select(r => r.Name).ShouldBe(["Hide All"]);
        result.Ruleset.Rules.Count.ShouldBe(1);
        result.Warnings.ShouldBeEmpty();
        result.OverBudget.ShouldBeFalse();
    }

    [Fact]
    public void Merge_better_gear_multiple_trailing_hideall_only_strips_last_one()
    {
        // Documents a gap: the merger only strips ONE trailing Hide-All from better-gear. If the
        // generator's own single-trailing-HideAll invariant is ever violated, an interior Hide-All
        // survives into the output, breaking the class's "exactly one Hide-All" guarantee.
        var betterGear = new FilterRuleset("BG", [Rule("U"), HideAll("H1"), HideAll("H2")]);

        var result = Merger.Merge([], betterGear, []);

        result.Ruleset.Rules.Select(r => r.Name).ShouldBe(["U", "H1", "Hide All"]);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.HideAll).ShouldBe(2);
    }

    [Fact]
    public void Merge_override_block_multiple_hideall_all_dropped_with_warnings()
    {
        var betterGear = BetterGear("U");

        var result = Merger.Merge([Rule("O1"), HideAll("H1"), Rule("O2"), HideAll("H2")], betterGear, []);

        result.Ruleset.Rules.Select(r => r.Name).ShouldBe(["O1", "O2", "U", "Hide All"]);
        result.Warnings.Count.ShouldBe(2);
        result.Warnings[0].ShouldContain("H1");
        result.Warnings[1].ShouldContain("H2");
    }

    [Fact]
    public void Merge_overriddenby_block_interior_hideall_is_dropped_with_warning()
    {
        // Symmetric with the override block: an interior Hide-All in the overridden-by block would hide
        // every rule below it, so it is dropped with a warning. The block's own *trailing* Hide-All is
        // preserved as the single final catch-all, upholding the "exactly one Hide-All" guarantee.
        var betterGear = BetterGear("U");

        var result = Merger.Merge([], betterGear, [Rule("X1"), HideAll("XH1"), Rule("X2"), HideAll("XH2")]);

        result.Ruleset.Rules.Select(r => r.Name).ShouldBe(["U", "X1", "X2", "XH2"]);
        result.Ruleset.Rules.Count(r => r.Visibility == Visibility.HideAll).ShouldBe(1);
        result.Ruleset.Rules[^1].Name.ShouldBe("XH2");
        result.Warnings.Count.ShouldBe(1);
        result.Warnings[0].ShouldContain("XH1");
        result.Warnings[0].ShouldContain("overridden-by block");
    }

    [Fact]
    public void Merge_reserved_one_under_cap_fits_exactly_with_no_warnings()
    {
        var overrideBlock = Enumerable.Range(1, 23).Select(i => Rule($"O{i}")).ToList();
        var betterGear = BetterGear("U");

        var result = Merger.Merge(overrideBlock, betterGear, []);

        result.Ruleset.Rules.Count.ShouldBe(FilterRuleset.MaxRuleCount);
        result.Ruleset.Rules.Select(r => r.Name).ShouldContain("U");
        result.Warnings.ShouldBeEmpty();
        result.OverBudget.ShouldBeFalse();
    }

    [Fact]
    public void Merge_reserved_exactly_at_cap_drops_all_better_gear_without_exceeding_warning()
    {
        var overrideBlock = Enumerable.Range(1, 24).Select(i => Rule($"O{i}")).ToList();
        var betterGear = BetterGear("U");

        var result = Merger.Merge(overrideBlock, betterGear, []);

        result.Ruleset.Rules.Count.ShouldBe(FilterRuleset.MaxRuleCount);
        result.Ruleset.Rules.Select(r => r.Name).ShouldNotContain("U");
        result.Warnings.Count.ShouldBe(1);
        result.Warnings[0].ShouldContain("dropped better-gear rule");
        result.Warnings.ShouldNotContain(w => w.Contains("exceeding the"));
        result.OverBudget.ShouldBeFalse();
    }

    [Fact]
    public void Merge_reserved_one_over_cap_clamps_budget_and_sets_overbudget()
    {
        var overrideBlock = Enumerable.Range(1, 25).Select(i => Rule($"O{i}")).ToList();
        var betterGear = BetterGear("U", "S");

        var result = Merger.Merge(overrideBlock, betterGear, []);

        result.Ruleset.Rules.Count.ShouldBe(26);
        result.Ruleset.Rules.Select(r => r.Name).ShouldNotContain("U");
        result.Ruleset.Rules.Select(r => r.Name).ShouldNotContain("S");
        result.Warnings.Count.ShouldBe(3);
        result.Warnings.Count(w => w.Contains("exceeding the")).ShouldBe(1);
        result.Warnings.Count(w => w.Contains("dropped better-gear rule")).ShouldBe(2);
        result.OverBudget.ShouldBeTrue();
    }

    [Fact]
    public void Merge_default_filter_name_is_progression_filter()
    {
        var result = Merger.Merge([], BetterGear(), []);

        result.Ruleset.Name.ShouldBe("Progression Filter");
    }

    [Fact]
    public void Merge_custom_filter_name_is_used()
    {
        var result = Merger.Merge([], BetterGear(), [], "My Barb Filter");

        result.Ruleset.Name.ShouldBe("My Barb Filter");
    }

    [Fact]
    public void Merge_empty_filter_name_passes_through_unchanged()
    {
        var result = Merger.Merge([], BetterGear(), [], "");

        result.Ruleset.Name.ShouldBe("");
    }

    [Fact]
    public void Merged_ruleset_at_cap_round_trips_through_codec()
    {
        var bodyNames = Enumerable.Range(1, 30).Select(i => $"BG{i}").ToArray();
        var betterGear = BetterGear(bodyNames);

        var result = Merger.Merge([], betterGear, []);
        var decoded = FilterCodec.Decode(FilterCodec.Encode(result.Ruleset));

        decoded.Rules.Count.ShouldBe(FilterRuleset.MaxRuleCount);
        decoded.Rules.Select(r => r.Name).ShouldBe(result.Ruleset.Rules.Select(r => r.Name));
        decoded.Rules[^1].Visibility.ShouldBe(Visibility.HideAll);
    }
}

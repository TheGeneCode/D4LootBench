using D4LootBench.App.ViewModels.Progression;
using D4LootBench.Core.Codec;
using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using D4LootBench.Core.Import;
using D4LootBench.Core.Progression;
using Shouldly;

namespace D4LootBench.App.Tests.Progression;

public sealed class ProgressionWizardViewModelTests
{
    // Maxroll-style guide (first line is a slot keyword → auto-detects) with Helm + Gloves both
    // targeting the helm's affixes.
    private const string Guide =
        "Helm\nCritical Strike Chance\nMaximum Life\nCooldown Reduction\n" +
        "Gloves\nCritical Strike Chance\nMaximum Life\nCooldown Reduction\n";

    // Mirrors the legendary-helm parser fixture: slot Helm + three resolvable affixes.
    private static readonly IReadOnlyList<string> HelmLines =
    [
        "Ancestral Legendary Helm",
        "925 Item Power",
        "+45.0% Critical Strike Chance",
        "+112 Maximum Life",
        "+8.5% Cooldown Reduction",
        "Requires Level 60",
    ];

    private static ProgressionWizardViewModel NewVm(
        IReadOnlyList<string> lines, Action<string>? clipboard = null)
        => NewVmWithReader(new FakeGearReader(lines), clipboard);

    private static ProgressionWizardViewModel NewVmWithReader(
        IGearReader reader, Action<string>? clipboard = null)
    {
        var data = new FilterDataService();
        return new ProgressionWizardViewModel(
            reader,
            new GearTooltipParser(data),
            new BuildGuideImporter(),
            new GoalBuildFactory(new NameResolver(data)),
            new SlotDiffEngine(),
            new ProgressionFilterGenerator(new NameResolver(data)),
            clipboard);
    }

    // Read failure seam: throws instead of returning OCR lines, to exercise AddGearFromImageAsync's
    // catch block without touching Windows OCR.
    private sealed class ThrowingGearReader : IGearReader
    {
        public Task<IReadOnlyList<string>> ReadLinesAsync(Stream image, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("OCR unavailable.");
    }

    // Returns a different line set on each call, so successive AddGearFromImageAsync calls produce
    // distinguishable items (used to verify RemoveItem keeps Items/_parsed in index sync).
    private sealed class SequenceGearReader(params IReadOnlyList<string>[] lineSets) : IGearReader
    {
        private int _index;

        public Task<IReadOnlyList<string>> ReadLinesAsync(Stream image, CancellationToken cancellationToken = default)
            => Task.FromResult(lineSets[_index++]);
    }

    [Fact]
    public async Task Wizard_read_adds_item_and_enables_next()
    {
        var vm = NewVm(HelmLines);

        await vm.AddGearFromImageAsync(new MemoryStream());

        vm.Items.Count.ShouldBe(1);
        vm.NextToReviewCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task Wizard_review_rebuilds_authoritative_session()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        await vm.AddGearFromImageAsync(new MemoryStream());

        vm.NextToReviewCommand.Execute(null);
        vm.Items[1].Slot = GearSlot.Gloves; // correct the second item's slot

        vm.PastedText = Guide;
        vm.GenerateCommand.Execute(null);

        // Corrected slot flowed into the diff: the Gloves goal is now satisfied by the corrected item,
        // so no Gloves rule is emitted (it would appear if the edit had not reached the session).
        vm.GeneratedRuleset.ShouldNotBeNull();
        vm.GeneratedRuleset!.Rules.ShouldNotContain(r => r.Name == "Gloves");
    }

    [Fact]
    public async Task Wizard_generate_produces_decodable_share_code()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;

        vm.GenerateCommand.Execute(null);

        vm.ShareCode.ShouldNotBeNullOrEmpty();
        FilterCodec.Decode(vm.ShareCode).Rules.Count.ShouldBe(vm.GeneratedRuleset!.Rules.Count);
    }

    [Fact]
    public async Task Wizard_generate_disabled_without_gear_or_text()
    {
        var vm = NewVm(HelmLines);
        vm.GenerateCommand.CanExecute(null).ShouldBeFalse();

        vm.PastedText = Guide; // text but no session yet
        vm.GenerateCommand.CanExecute(null).ShouldBeFalse();

        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.GenerateCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task Wizard_copy_uses_injected_clipboard()
    {
        string? captured = null;
        var vm = NewVm(HelmLines, s => captured = s);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.GenerateCommand.Execute(null);

        vm.CopyCodeCommand.Execute(null);

        captured.ShouldBe(vm.ShareCode);
    }

    [Fact]
    public async Task Wizard_add_gear_read_failure_reports_error_and_adds_no_item()
    {
        var vm = NewVmWithReader(new ThrowingGearReader());

        await vm.AddGearFromImageAsync(new MemoryStream());

        vm.Items.ShouldBeEmpty();
        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldContain("Read failed");
        vm.NextToReviewCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task Wizard_remove_item_not_in_collection_is_noop()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        var detachedSession = new GearReviewSession([new GearTooltipParser(new FilterDataService()).Parse(HelmLines)]);
        var detached = new GearItemDraftViewModel(detachedSession.Items[0]);

        vm.RemoveItemCommand.Execute(detached);

        vm.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Wizard_remove_item_keeps_parsed_list_in_index_sync()
    {
        var lines925 = HelmLines;
        var lines111 = HelmLines.Select(l => l == "925 Item Power" ? "111 Item Power" : l).ToList();
        var lines222 = HelmLines.Select(l => l == "925 Item Power" ? "222 Item Power" : l).ToList();
        var vm = NewVmWithReader(new SequenceGearReader(lines925, lines111, lines222));

        await vm.AddGearFromImageAsync(new MemoryStream());
        await vm.AddGearFromImageAsync(new MemoryStream());
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.Items.Select(i => i.ItemPower).ShouldBe(new int?[] { 925, 111, 222 });

        vm.RemoveItemCommand.Execute(vm.Items[1]); // remove the middle (111) item
        vm.Items.Select(i => i.ItemPower).ShouldBe(new int?[] { 925, 222 });

        // Rebuild the authoritative session from _parsed; if RemoveItem hadn't kept _parsed in index
        // sync with Items, the removed 111 item would reappear here.
        vm.NextToReviewCommand.Execute(null);

        vm.Items.Select(i => i.ItemPower).ShouldBe(new int?[] { 925, 222 });
    }

    [Fact]
    public async Task Wizard_removing_item_after_review_invalidates_session_for_generate()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.GenerateCommand.CanExecute(null).ShouldBeTrue();

        vm.RemoveItemCommand.Execute(vm.Items[0]);

        vm.GenerateCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task Wizard_generate_reports_error_on_undetectable_guide_format()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.NextToGoalCommand.Execute(null);
        vm.PastedText = "this text matches no known build-guide format at all";

        vm.GenerateCommand.Execute(null);

        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldContain("Format could not be detected");
        vm.ShareCode.ShouldBeEmpty();
        vm.CurrentStep.ShouldBe(ProgressionStep.Goal); // stays put — failed generate doesn't advance to Result
    }

    [Fact]
    public async Task Wizard_start_over_resets_all_state()
    {
        var vm = NewVm(HelmLines);
        await vm.AddGearFromImageAsync(new MemoryStream());
        vm.NextToReviewCommand.Execute(null);
        vm.PastedText = Guide;
        vm.GenerateCommand.Execute(null);
        vm.ShareCode.ShouldNotBeNullOrEmpty(); // sanity: generation happened before reset

        vm.StartOverCommand.Execute(null);

        vm.Items.ShouldBeEmpty();
        vm.PastedText.ShouldBeEmpty();
        vm.ShareCode.ShouldBeEmpty();
        vm.Warnings.ShouldBeEmpty();
        vm.GeneratedRuleset.ShouldBeNull();
        vm.CurrentStep.ShouldBe(ProgressionStep.ReadGear);
        vm.NextToReviewCommand.CanExecute(null).ShouldBeFalse();
        vm.GenerateCommand.CanExecute(null).ShouldBeFalse();
    }
}

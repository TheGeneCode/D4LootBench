using D4LootBench.App.ViewModels.Progression;
using D4LootBench.Core.Data;
using D4LootBench.Core.Gear;
using Shouldly;

namespace D4LootBench.App.Tests.Progression;

public sealed class GearDraftViewModelTests
{
    private static readonly IReadOnlyList<string> HelmLines =
    [
        "Ancestral Legendary Helm",
        "925 Item Power",
        "+45.0% Critical Strike Chance",
        "+112 Maximum Life",
        "+8.5% Cooldown Reduction",
        "Requires Level 60",
    ];

    private static GearReviewSession NewSession()
    {
        var result = new GearTooltipParser(new FilterDataService()).Parse(HelmLines);
        return new GearReviewSession([result]);
    }

    [Fact]
    public void Slot_edit_writes_through_to_build()
    {
        var session = NewSession();
        var vm = new GearItemDraftViewModel(session.Items[0]);

        vm.Slot = GearSlot.Gloves;

        session.Build()[0].Slot.ShouldBe(GearSlot.Gloves);
    }

    [Fact]
    public void ToggleGreaterAffix_flows_to_build()
    {
        var session = NewSession();
        var vm = new GearItemDraftViewModel(session.Items[0]);

        vm.Affixes[0].IsGreaterAffix = true;

        session.Build()[0].Affixes[0].IsGreaterAffix.ShouldBeTrue();
    }

    [Fact]
    public void IsGreaterAffix_raises_property_changed()
    {
        var session = NewSession();
        var vm = new GearAffixDraftViewModel(session.Items[0].Affixes[0]);
        var raised = false;
        vm.PropertyChanged += (_, e) => raised = e.PropertyName == nameof(vm.IsGreaterAffix);

        vm.IsGreaterAffix = true;

        raised.ShouldBeTrue();
    }
}

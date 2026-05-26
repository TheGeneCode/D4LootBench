using CommunityToolkit.Mvvm.ComponentModel;
using D4LootBench.Core.Data;
using D4LootBench.Core.Models;

namespace D4LootBench.App.ViewModels.Conditions;

public sealed partial class SpecificUniqueConditionViewModel : ConditionViewModel
{
    private readonly IFilterDataService _data;

    public PickerViewModel Picker { get; }

    private readonly ILookup<string, uint> _displayNameToSnoIds;

    public SpecificUniqueConditionViewModel(IFilterDataService data)
    {
        _data = data;
        _displayNameToSnoIds = _data.Uniques.Released
            .ToLookup(e => e.Name, e => e.SnoId);

        var source = _displayNameToSnoIds
            .Select(g => new PickerEntry(g.First(), g.Key))
            .OrderBy(e => e.DisplayName)
            .ToList();

        Picker = new PickerViewModel(source)
        {
            MaxSelectionCount = SpecificUniqueCondition.MaxSelectionCount
        };
        Picker.Selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    public SpecificUniqueConditionViewModel(IFilterDataService data, SpecificUniqueCondition m) : this(data)
    {
        var seen = new HashSet<string>();
        foreach (var id in m.UniqueIds)
        {
            var name = _data.Uniques.GetDisplayName(id);
            if (seen.Add(name))
                Picker.Selected.Add(new PickerEntry(id, name));
        }
    }

    public override void ApplyClassFilter(PlayerClass playerClass)
    {
        if (playerClass == PlayerClass.All)
            Picker.SourceFilter = null;
        else
        {
            var allowed = _data.Uniques.ForClass(playerClass.ToString())
                .Select(e => e.SnoId)
                .ToHashSet();
            Picker.SourceFilter = e => allowed.Contains(e.Hash);
        }
    }

    public override string TypeName => "Specific Unique";
    public override string Summary =>
        $"{Picker.Selected.Count} unique{(Picker.Selected.Count == 1 ? "" : "s")}";

    public override Condition BuildModel() =>
        new SpecificUniqueCondition(
            Picker.Selected
                .SelectMany(e => _displayNameToSnoIds[e.DisplayName])
                .Distinct()
                .ToList());
}

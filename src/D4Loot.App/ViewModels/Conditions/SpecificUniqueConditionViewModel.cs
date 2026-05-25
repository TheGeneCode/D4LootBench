using CommunityToolkit.Mvvm.ComponentModel;
using D4Loot.Core.Data;
using D4Loot.Core.Models;

namespace D4Loot.App.ViewModels.Conditions;

public sealed partial class SpecificUniqueConditionViewModel : ConditionViewModel
{
    public PickerViewModel Picker { get; }

    private readonly ILookup<string, uint> _displayNameToSnoIds;

    public SpecificUniqueConditionViewModel()
    {
        _displayNameToSnoIds = UniqueItemDatabase.Released
            .ToLookup(e => e.Name, e => e.SnoId);

        var source = _displayNameToSnoIds
            .Select(g => new PickerEntry(g.First(), g.Key))
            .OrderBy(e => e.DisplayName)
            .ToList();

        Picker = new PickerViewModel(source);
        Picker.Selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    public SpecificUniqueConditionViewModel(SpecificUniqueCondition m) : this()
    {
        var seen = new HashSet<string>();
        foreach (var id in m.UniqueIds)
        {
            var name = UniqueItemDatabase.GetDisplayName(id);
            if (seen.Add(name))
                Picker.Selected.Add(new PickerEntry(id, name));
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

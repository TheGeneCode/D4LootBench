using CommunityToolkit.Mvvm.ComponentModel;
using ThunderEagle.FilterForge.Core.Data;
using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels.Conditions;

public sealed partial class ItemTypeConditionViewModel : ConditionViewModel
{
    private readonly IFilterDataService _data;

    public PickerViewModel Picker { get; }

    public ItemTypeConditionViewModel(IFilterDataService data)
    {
        _data = data;
        Picker = new PickerViewModel(
            _data.ItemTypes.All.Select(e => new PickerEntry(e.Hash, e.Name)));
        Picker.Selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    public ItemTypeConditionViewModel(IFilterDataService data, ItemTypeCondition m) : this(data)
    {
        foreach (var id in m.TypeIds)
            Picker.Selected.Add(new PickerEntry(id, _data.ItemTypes.GetDisplayName(id)));
    }

    public override void ApplyClassFilter(PlayerClass playerClass)
    {
        if (playerClass == PlayerClass.All)
            Picker.SourceFilter = null;
        else
        {
            var allowed = _data.ItemTypes.ForClass(playerClass.ToString())
                .Select(e => e.Hash)
                .ToHashSet();
            Picker.SourceFilter = e => allowed.Contains(e.Hash);
        }
    }

    public override string TypeName => "Item Type";
    public override string Summary =>
        $"{Picker.Selected.Count} type{(Picker.Selected.Count == 1 ? "" : "s")}";

    public override Condition BuildModel() =>
        new ItemTypeCondition(Picker.Selected.Select(e => e.Hash).ToList());
}

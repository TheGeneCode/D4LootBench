using CommunityToolkit.Mvvm.ComponentModel;
using ThunderEagle.FilterForge.Core.Data;
using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels.Conditions;

public sealed partial class OptionalAffixConditionViewModel : ConditionViewModel
{
    private readonly IFilterDataService _data;

    // GreaterEntries and Field5 semantics not fully understood — preserved for lossless round-trips
    private readonly IReadOnlyList<GreaterAffixEntry> _preservedGreaterEntries;
    private readonly int _preservedField5;

    public PickerViewModel Picker { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private int _minimumCount;

    public OptionalAffixConditionViewModel(IFilterDataService data)
    {
        _data = data;
        _preservedGreaterEntries = [];
        Picker = MakePicker();
        Picker.Selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    public OptionalAffixConditionViewModel(IFilterDataService data, OptionalAffixCondition m)
    {
        _data = data;
        _minimumCount            = m.MinimumCount;
        _preservedGreaterEntries = m.GreaterEntries;
        _preservedField5         = m.Field5;
        Picker = MakePicker();
        foreach (var id in m.AffixIds)
            Picker.Selected.Add(new PickerEntry(id, _data.Affixes.GetDisplayName(id)));
        Picker.Selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    private PickerViewModel MakePicker() =>
        new(_data.Affixes.ByHash.Select(kv => new PickerEntry(kv.Key, kv.Value.Name)))
        {
            MaxSelectionCount = OptionalAffixCondition.MaxSelectionCount
        };

    public override void ApplyClassFilter(PlayerClass playerClass)
    {
        if (playerClass == PlayerClass.All)
            Picker.SourceFilter = null;
        else
        {
            var allowed = _data.Affixes.ForClass(playerClass.ToString())
                .Select(e => e.Hash)
                .ToHashSet();
            Picker.SourceFilter = e => allowed.Contains(e.Hash);
        }
    }

    public override string TypeName => "Optional Affixes";
    public override string Summary => MinimumCount > 0
        ? $"min {MinimumCount} of {Picker.Selected.Count}"
        : $"any of {Picker.Selected.Count}";

    public override Condition BuildModel() =>
        new OptionalAffixCondition(Picker.Selected.Select(e => e.Hash).ToList(), MinimumCount)
        {
            GreaterEntries = _preservedGreaterEntries,
            Field5         = _preservedField5
        };
}

using CommunityToolkit.Mvvm.ComponentModel;
using D4Loot.Core.Data;
using D4Loot.Core.Models;

namespace D4Loot.App.ViewModels.Conditions;

public sealed partial class OptionalAffixConditionViewModel : ConditionViewModel
{
    // GreaterEntries and Field5 semantics not fully understood — preserved for lossless round-trips
    private readonly IReadOnlyList<GreaterAffixEntry> _preservedGreaterEntries;
    private readonly int _preservedField5;

    public PickerViewModel Picker { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private int _minimumCount;

    public OptionalAffixConditionViewModel()
    {
        _preservedGreaterEntries = [];
        Picker = MakePicker();
        Picker.Selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    public OptionalAffixConditionViewModel(OptionalAffixCondition m)
    {
        _minimumCount            = m.MinimumCount;
        _preservedGreaterEntries = m.GreaterEntries;
        _preservedField5         = m.Field5;
        Picker = MakePicker();
        foreach (var id in m.AffixIds)
            Picker.Selected.Add(new PickerEntry(id, AffixDatabase.GetDisplayName(id)));
        Picker.Selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    private PickerViewModel MakePicker() =>
        new(AffixDatabase.ByHash.Select(kv => new PickerEntry(kv.Key, kv.Value.Name)))
        {
            MaxSelectionCount = OptionalAffixCondition.MaxSelectionCount
        };

    public override void ApplyClassFilter(PlayerClass playerClass)
    {
        if (playerClass == PlayerClass.All)
            Picker.SourceFilter = null;
        else
        {
            var allowed = AffixDatabase.ForClass(playerClass.ToString())
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

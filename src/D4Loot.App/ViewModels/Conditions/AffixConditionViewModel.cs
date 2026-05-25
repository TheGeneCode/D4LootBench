using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using D4Loot.Core.Data;
using D4Loot.Core.Models;

namespace D4Loot.App.ViewModels.Conditions;

public sealed partial class AffixConditionViewModel : ConditionViewModel
{
    private readonly Dictionary<uint, uint> _preservedGreaterValues = [];
    private readonly int _preservedField5;

    public PickerViewModel Picker { get; }
    public PickerViewModel GreaterPicker { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private int _minimumCount = 1;

    public AffixConditionViewModel()
    {
        _preservedField5 = 0;
        Picker = MakePicker();
        GreaterPicker = MakeGreaterPicker();
        Picker.Selected.CollectionChanged += OnPickerSelectionChanged;
        Picker.Selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
        GreaterPicker.Selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    public AffixConditionViewModel(AffixCondition m)
    {
        _minimumCount = m.MinimumCount;
        _preservedField5 = m.Field5;
        foreach (var ge in m.GreaterEntries)
        {
            if (!_preservedGreaterValues.ContainsKey(ge.AffixId))
                _preservedGreaterValues[ge.AffixId] = ge.Value;
        }

        Picker = MakePicker();
        foreach (var id in m.AffixIds)
            Picker.Selected.Add(new PickerEntry(id, AffixDatabase.GetDisplayName(id)));

        GreaterPicker = MakeGreaterPicker();
        foreach (var ge in m.GreaterEntries)
            GreaterPicker.Selected.Add(ToPickerEntry(ge));

        Picker.Selected.CollectionChanged += OnPickerSelectionChanged;
        Picker.Selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
        GreaterPicker.Selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    private void OnPickerSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var source = Picker.Selected.Select(p => new PickerEntry(p.Hash, p.DisplayName));
        GreaterPicker.ReplaceSource(source);
    }

    private PickerViewModel MakePicker() =>
        new(AffixDatabase.ByHash.Select(kv => new PickerEntry(kv.Key, kv.Value.Name)))
        {
            MaxSelectionCount = AffixCondition.MaxSelectionCount
        };

    private PickerViewModel MakeGreaterPicker() =>
        new(Enumerable.Empty<PickerEntry>())
        {
            MaxSelectionCount = AffixCondition.MaxSelectionCount
        };

    private static PickerEntry ToPickerEntry(GreaterAffixEntry ge) =>
        new(ge.AffixId, AffixDatabase.GetDisplayName(ge.AffixId));

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

    public override string TypeName => "Required Affixes";

    public override string Summary
    {
        get
        {
            var ga = GreaterPicker.Selected.Count;
            var s = $"min {MinimumCount} of {Picker.Selected.Count}";
            return ga > 0 ? $"{s}, {ga} greater" : s;
        }
    }

    public override Condition BuildModel() =>
        new AffixCondition(Picker.Selected.Select(e => e.Hash).ToList(), MinimumCount)
        {
            GreaterEntries = GreaterPicker.Selected
                .Select(e => new GreaterAffixEntry(e.Hash,
                    _preservedGreaterValues.TryGetValue(e.Hash, out var v) ? v : 0))
                .ToList(),
            Field5 = _preservedField5
        };
}

using CommunityToolkit.Mvvm.ComponentModel;
using D4Loot.Core.Models;

namespace D4Loot.App.ViewModels.Conditions;

public sealed partial class ItemPowerConditionViewModel : ConditionViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private int _minimum;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private int _maximum;

    public ItemPowerConditionViewModel() { }

    public ItemPowerConditionViewModel(ItemPowerCondition m)
    {
        _minimum = m.Minimum;
        _maximum = m.Maximum > 900 ? 900 : m.Maximum;
    }

    partial void OnMaximumChanged(int value)
    {
        if (value > 900)
            Maximum = 900;
    }

    public override string TypeName => "Item Power";
    public override string Summary => Maximum == 0 ? $"{Minimum}+" : $"{Minimum} – {Maximum}";
    public override Condition BuildModel() => new ItemPowerCondition(Minimum, Maximum);
}

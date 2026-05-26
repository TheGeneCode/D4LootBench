using CommunityToolkit.Mvvm.ComponentModel;
using D4LootBench.Core.Models;

namespace D4LootBench.App.ViewModels.Conditions;

public sealed partial class ItemPowerConditionViewModel : ConditionViewModel
{
    public const int GameCap = 900;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private int _minimum;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    [NotifyPropertyChangedFor(nameof(MaximumWasClamped))]
    private int _maximum;

    public ItemPowerConditionViewModel() { }

    public ItemPowerConditionViewModel(ItemPowerCondition m)
    {
        _minimum = m.Minimum;
        _maximum = m.Maximum > GameCap ? GameCap : m.Maximum;
    }

    /// <summary>True when the user just typed something the model auto-clamped, so we can surface a hint.</summary>
    public bool MaximumWasClamped { get; private set; }

    partial void OnMaximumChanging(int value)
    {
        // Track the clamp *before* the property setter applies our coerced value.
        MaximumWasClamped = value > GameCap;
    }

    partial void OnMaximumChanged(int value)
    {
        if (value > GameCap)
            Maximum = GameCap;
    }

    public override string TypeName => "Item Power";
    public override string Summary => Maximum == 0 ? $"{Minimum}+" : $"{Minimum} – {Maximum}";
    public override Condition BuildModel() => new ItemPowerCondition(Minimum, Maximum);
}

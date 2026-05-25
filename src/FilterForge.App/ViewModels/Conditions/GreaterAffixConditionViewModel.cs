using CommunityToolkit.Mvvm.ComponentModel;
using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels.Conditions;

public sealed partial class GreaterAffixConditionViewModel : ConditionViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private int _minimumCount;

    public GreaterAffixConditionViewModel() { }

    public GreaterAffixConditionViewModel(GreaterAffixCondition m)
    {
        var clamped = m.MinimumCount;
        if (clamped < 1) clamped = 1;
        if (clamped > 4) clamped = 4;
        _minimumCount = clamped;
    }

    partial void OnMinimumCountChanged(int value)
    {
        if (value < 1)
            MinimumCount = 1;
        else if (value > 4)
            MinimumCount = 4;
    }

    public override string TypeName => "Greater Affix";
    public override string Summary => $"Min {MinimumCount}";
    public override Condition BuildModel() => new GreaterAffixCondition(MinimumCount);
}

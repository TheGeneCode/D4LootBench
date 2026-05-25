using CommunityToolkit.Mvvm.ComponentModel;
using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels.Conditions;

public sealed partial class ItemPropertiesConditionViewModel : ConditionViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    [NotifyPropertyChangedFor(nameof(IsAncestral))]
    private int _propertyMask = 1;

    public ItemPropertiesConditionViewModel() { }

    public ItemPropertiesConditionViewModel(ItemPropertiesCondition m) =>
        _propertyMask = m.PropertyMask;

    public bool IsAncestral
    {
        get => PropertyMask == 4;
        set => PropertyMask = value ? 4 : 1;
    }

    public override string TypeName => "Item Properties";
    public override string Summary => PropertyMask == 4 ? "Ancestral" : $"Mask = {PropertyMask}";
    public override Condition BuildModel() => new ItemPropertiesCondition(PropertyMask);
}

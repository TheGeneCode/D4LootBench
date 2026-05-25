using CommunityToolkit.Mvvm.ComponentModel;
using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels.Conditions;

public sealed partial class RarityConditionViewModel : ConditionViewModel
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Summary))] private bool _common;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Summary))] private bool _magic;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Summary))] private bool _rare;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Summary))] private bool _legendary;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Summary))] private bool _unique;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Summary))] private bool _mythic;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Summary))] private bool _talisman;

    public RarityConditionViewModel() { }

    public RarityConditionViewModel(RarityCondition m)
    {
        _common    = m.Mask.HasFlag(RarityFlags.Common);
        _magic     = m.Mask.HasFlag(RarityFlags.Magic);
        _rare      = m.Mask.HasFlag(RarityFlags.Rare);
        _legendary = m.Mask.HasFlag(RarityFlags.Legendary);
        _unique    = m.Mask.HasFlag(RarityFlags.Unique);
        _mythic    = m.Mask.HasFlag(RarityFlags.Mythic);
        _talisman  = m.Mask.HasFlag(RarityFlags.Talisman);
    }

    public RarityFlags Mask =>
        (Common    ? RarityFlags.Common    : 0) |
        (Magic     ? RarityFlags.Magic     : 0) |
        (Rare      ? RarityFlags.Rare      : 0) |
        (Legendary ? RarityFlags.Legendary : 0) |
        (Unique    ? RarityFlags.Unique    : 0) |
        (Mythic    ? RarityFlags.Mythic    : 0) |
        (Talisman  ? RarityFlags.Talisman  : 0);

    public override string TypeName => "Rarity";
    public override string Summary => ConditionViewModelHelpers.FormatRarityFlags(Mask);
    public override Condition BuildModel() => new RarityCondition(Mask);
}

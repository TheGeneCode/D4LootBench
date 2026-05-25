using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThunderEagle.FilterForge.App.Utilities;
using ThunderEagle.FilterForge.App.ViewModels.Conditions;
using ThunderEagle.FilterForge.App.Views;
using ThunderEagle.FilterForge.Core.Data;
using ThunderEagle.FilterForge.Core.Models;

namespace ThunderEagle.FilterForge.App.ViewModels;

/// <summary>A named color entry used in the color swatch palette.</summary>
public sealed class NamedColor(string name, uint argb)
{
    public string          Name  { get; } = name;
    public uint            Argb  { get; } = argb;
    public SolidColorBrush Brush { get; } = new(ColorUtility.ArgbToWpf(argb));
}

public partial class FilterRuleViewModel : ObservableObject
{
    private readonly IConditionViewModelFactory _conditionFactory;
    private readonly Func<FilterRuleViewModel, IEnumerable<uint>> _getPeerColors;
    private SolidColorBrush? _wpfBrush;
    private PlayerClass _classFilter = PlayerClass.All;

    [ObservableProperty] private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecolor))]
    [NotifyPropertyChangedFor(nameof(DisplayBrush))]
    private Visibility _visibility;

    [ObservableProperty] private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WpfBrush))]
    [NotifyPropertyChangedFor(nameof(DisplayBrush))]
    [NotifyPropertyChangedFor(nameof(ColorHex))]
    private uint _color;

    public ObservableCollection<ConditionViewModel> Conditions { get; } = [];

    public static IReadOnlyList<NamedColor> StandardColors { get; } =
    [
        new("Game Default",   FilterColors.GameDefault),
        new("Blue",           FilterColors.Blue),
        new("Cyan",           FilterColors.Cyan),
        new("Green",          FilterColors.Green),
        new("Orange",         FilterColors.Orange),
        new("Gold",           FilterColors.Gold)
    ];

    public static Visibility[] VisibilityValues { get; } = Enum.GetValues<Visibility>();

    public bool IsRecolor => Visibility == Visibility.Recolor;

    private static readonly SolidColorBrush GameDefaultBrush =
        new(ColorUtility.ArgbToWpf(FilterColors.GameDefault));

    public SolidColorBrush DisplayBrush => IsRecolor ? WpfBrush : GameDefaultBrush;

    public SolidColorBrush WpfBrush
    {
        get
        {
            _wpfBrush ??= new SolidColorBrush(ColorUtility.ArgbToWpf(Color));
            return _wpfBrush;
        }
    }

    public string ColorHex
    {
        // Display as 6-char RRGGBB to match the in-game color picker format (alpha omitted, always FF).
        get => $"{Color >> 16 & 0xFF:X2}{Color >> 8 & 0xFF:X2}{Color & 0xFF:X2}";
        set
        {
            var s = value.TrimStart('#');
            Color = s.Length switch
            {
                6 when uint.TryParse(s, NumberStyles.HexNumber, null, out var rgb)  => 0xFF000000u | rgb,
                8 when uint.TryParse(s, NumberStyles.HexNumber, null, out var argb) => argb,
                _ => Color
            };
        }
    }

    [ObservableProperty]
    private ConditionType _selectedNewConditionType;

    public IEnumerable<ConditionType> AvailableConditionTypes =>
        Enum.GetValues<ConditionType>().Where(t => Conditions.All(c => _conditionFactory.GetConditionType(c) != t));

    public FilterRuleViewModel(
        IConditionViewModelFactory conditionFactory,
        FilterRule rule,
        Func<FilterRuleViewModel, IEnumerable<uint>>? getPeerColors = null)
    {
        _conditionFactory = conditionFactory;
        _name             = rule.Name;
        _visibility       = rule.Visibility;
        _color            = rule.Color;
        _isEnabled        = rule.IsEnabled;
        _getPeerColors    = getPeerColors ?? (_ => []);

        foreach (var condition in rule.Conditions)
            Conditions.Add(_conditionFactory.FromModel(condition));

        Conditions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(AvailableConditionTypes));
    }

    public void ApplyClassFilter(PlayerClass playerClass)
    {
        _classFilter = playerClass;
        foreach (var condition in Conditions)
            condition.ApplyClassFilter(playerClass);
    }

    public FilterRule BuildRule() =>
        new(Name, Visibility, Color, Conditions.Select(c => c.BuildModel()).ToList(), IsEnabled);

    [RelayCommand]
    private void AddCondition()
    {
        if (Conditions.Any(c => _conditionFactory.GetConditionType(c) == SelectedNewConditionType))
            return;

        var vm = _conditionFactory.CreateNew(SelectedNewConditionType);
        vm.ApplyClassFilter(_classFilter);
        Conditions.Add(vm);
    }

    // value unused intentionally — only need to invalidate the cached brush on any change.
    // ReSharper disable once UnusedParameter.Local
    partial void OnColorChanging(uint value) => _wpfBrush = null;

    [RelayCommand]
    private void SelectColor(uint argb) => Color = argb;

    // ReSharper disable once UnusedMember.Local — called by generated PickColorCommand
    [RelayCommand]
    private void PickColor()
    {
        var dialog = new ColorPickerDialog(Color == 0 ? FilterColors.Gold : Color)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dialog.ShowDialog() == true)
            Color = dialog.ResultColor;
    }

    [RelayCommand]
    private void GenerateDistinctColor() =>
        Color = ColorUtility.GenerateDistinctColor(_getPeerColors(this));

    [RelayCommand]
    private void DeleteCondition(ConditionViewModel condition)
    {
        var idx = Conditions.IndexOf(condition);
        if (idx < 0) return;
        _lastDeletedCondition = condition;
        _lastDeletedIndex     = idx;
        Conditions.RemoveAt(idx);
        UndoDeleteConditionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(LastDeletedConditionTypeName));
    }

    private ConditionViewModel? _lastDeletedCondition;
    private int _lastDeletedIndex;

    public bool HasUndoableDelete => _lastDeletedCondition is not null;

    public string LastDeletedConditionTypeName => _lastDeletedCondition?.TypeName ?? "";

    [RelayCommand(CanExecute = nameof(HasUndoableDelete))]
    private void UndoDeleteCondition()
    {
        if (_lastDeletedCondition is null) return;
        var insertAt = Math.Clamp(_lastDeletedIndex, 0, Conditions.Count);
        Conditions.Insert(insertAt, _lastDeletedCondition);
        _lastDeletedCondition = null;
        UndoDeleteConditionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(LastDeletedConditionTypeName));
        OnPropertyChanged(nameof(HasUndoableDelete));
    }
}

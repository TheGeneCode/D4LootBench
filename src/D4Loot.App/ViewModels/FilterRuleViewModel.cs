using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D4Loot.App.Utilities;
using D4Loot.App.Views;
using D4Loot.Core.Data;
using D4Loot.Core.Models;

namespace D4Loot.App.ViewModels;

/// <summary>A named color entry used in the color swatch palette.</summary>
public sealed class NamedColor(string name, uint argb)
{
    public string          Name  { get; } = name;
    public uint            Argb  { get; } = argb;
    public SolidColorBrush Brush { get; } = new(ColorUtility.ArgbToWpf(argb));
}

public partial class FilterRuleViewModel : ObservableObject
{
    private readonly Func<FilterRuleViewModel, IEnumerable<uint>> _getPeerColors;
    private SolidColorBrush? _wpfBrush;

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

    public FilterRuleViewModel(FilterRule rule, Func<FilterRuleViewModel, IEnumerable<uint>>? getPeerColors = null)
    {
        _name          = rule.Name;
        _visibility    = rule.Visibility;
        _color         = rule.Color;
        _isEnabled     = rule.IsEnabled;
        _getPeerColors = getPeerColors ?? (_ => []);

        foreach (var condition in rule.Conditions)
            Conditions.Add(new ConditionViewModel(condition));
    }

    public FilterRule BuildRule() =>
        new(Name, Visibility, Color, Conditions.Select(c => c.Model).ToList(), IsEnabled);

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
    private void DeleteCondition(ConditionViewModel condition) => Conditions.Remove(condition);
}

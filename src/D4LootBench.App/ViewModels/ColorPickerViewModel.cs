using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;
using D4LootBench.App.Utilities;
using D4LootBench.Core.Data;

namespace D4LootBench.App.ViewModels;

public sealed class ColorPickerViewModel : INotifyPropertyChanged
{
    private float  _hue;
    private float  _saturation;
    private float  _value;
    private string _hexText = "";
    private Color  _hueColor;
    private bool   _syncing;

    public event PropertyChangedEventHandler? PropertyChanged;

    // Fired after any HSV/hex change so the view can reposition cursors.
    public event EventHandler? HsvChanged;

    public SolidColorBrush PreviewBrush { get; } = new();
    public uint ResultArgb { get; private set; }

    public float Hue        => _hue;
    public float Saturation => _saturation;
    public float Value      => _value;

    public Color HueColor
    {
        get => _hueColor;
        private set { _hueColor = value; Notify(nameof(HueColor)); }
    }

    public string HexText
    {
        get => _hexText;
        set
        {
            if (_hexText == value) return;
            _hexText = value;
            Notify(nameof(HexText));
            if (!_syncing) ApplyFromHex(value);
        }
    }

    public ColorPickerViewModel(uint initialArgb)
    {
        if (initialArgb == 0) initialArgb = FilterColors.Gold;
        (_hue, _saturation, _value) = ColorUtility.ArgbToHsv(initialArgb);
        Apply();
    }

    public void SetHue(float h)
    {
        _hue = Math.Clamp(h, 0f, 359.99f);
        Apply();
    }

    public void SetSatVal(float s, float v)
    {
        _saturation = Math.Clamp(s, 0f, 1f);
        _value      = Math.Clamp(v, 0f, 1f);
        Apply();
    }

    private void ApplyFromHex(string hex)
    {
        var s = hex.TrimStart('#');
        if (s.Length != 6 || !uint.TryParse(s, NumberStyles.HexNumber, null, out var rgb)) return;
        (_hue, _saturation, _value) = ColorUtility.ArgbToHsv(0xFF000000u | rgb);
        Apply();
    }

    private void Apply()
    {
        ResultArgb         = ColorUtility.HsvToArgb(_hue, _saturation, _value);
        PreviewBrush.Color = ColorUtility.ArgbToWpf(ResultArgb);
        HueColor           = ColorUtility.ArgbToWpf(ColorUtility.HsvToArgb(_hue, 1f, 1f));

        _syncing = true;
        HexText  = $"{ResultArgb >> 16 & 0xFF:X2}{ResultArgb >> 8 & 0xFF:X2}{ResultArgb & 0xFF:X2}";
        _syncing = false;

        HsvChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

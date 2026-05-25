using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ThunderEagle.FilterForge.App.ViewModels;

namespace ThunderEagle.FilterForge.App.Views;

public partial class ColorPickerDialog : Window
{
    private readonly ColorPickerViewModel _vm;
    private bool _draggingSv;
    private bool _draggingHue;

    public uint ResultColor { get; private set; }

    public ColorPickerDialog(uint currentArgb)
    {
        InitializeComponent();
        _vm = new ColorPickerViewModel(currentArgb);
        DataContext = _vm;
        _vm.HsvChanged += OnHsvChanged;
        Loaded += (_, _) => RefreshCursors();
    }

    // ── Cursor positioning ────────────────────────────────────────────

    private void RefreshCursors()
    {
        UpdateSvCursor();
        UpdateHueCursor();
        HueGradientStop.Color = _vm.HueColor;
    }

    private void OnHsvChanged(object? sender, EventArgs e)
    {
        HueGradientStop.Color = _vm.HueColor;
        UpdateSvCursor();
        UpdateHueCursor();
    }

    private void UpdateSvCursor()
    {
        var w = SvCanvas.ActualWidth;
        var h = SvCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;
        Canvas.SetLeft(SvCursor, _vm.Saturation * w - SvCursor.Width  / 2);
        Canvas.SetTop (SvCursor, (1f - _vm.Value) * h - SvCursor.Height / 2);
    }

    private void UpdateHueCursor()
    {
        var h = HueCanvas.ActualHeight;
        if (h <= 0) return;
        Canvas.SetTop(HueCursor, _vm.Hue / 360f * h - HueCursor.Height / 2);
    }

    // ── SV canvas mouse ───────────────────────────────────────────────

    private void SvCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _draggingSv = true;
        SvCanvas.CaptureMouse();
        ApplySvPoint(e.GetPosition(SvCanvas));
    }

    private void SvCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingSv) return;
        ApplySvPoint(e.GetPosition(SvCanvas));
    }

    private void SvCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggingSv = false;
        SvCanvas.ReleaseMouseCapture();
    }

    private void ApplySvPoint(Point p)
    {
        var s = (float)Math.Clamp(p.X / SvCanvas.ActualWidth,  0, 1);
        var v = (float)Math.Clamp(1 - p.Y / SvCanvas.ActualHeight, 0, 1);
        _vm.SetSatVal(s, v);
    }

    // ── Hue canvas mouse ──────────────────────────────────────────────

    private void HueCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _draggingHue = true;
        HueCanvas.CaptureMouse();
        ApplyHuePoint(e.GetPosition(HueCanvas));
    }

    private void HueCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingHue) return;
        ApplyHuePoint(e.GetPosition(HueCanvas));
    }

    private void HueCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggingHue = false;
        HueCanvas.ReleaseMouseCapture();
    }

    private void ApplyHuePoint(Point p) =>
        _vm.SetHue((float)(p.Y / HueCanvas.ActualHeight * 360.0));

    // ── Hex input ─────────────────────────────────────────────────────

    private void HexTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        // Force binding update then move focus away so LostFocus doesn't double-fire
        var tb = (TextBox)sender;
        tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }

    // ── Dialog buttons ────────────────────────────────────────────────

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        ResultColor  = _vm.ResultArgb;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) =>
        DialogResult = false;
}

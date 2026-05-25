using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ThunderEagle.FilterForge.Core.Validation;

namespace ThunderEagle.FilterForge.App.Converters;

/// <summary>Maps <see cref="ValidationSeverity"/> to a glyph for inline display.</summary>
public sealed class ValidationSeverityGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ValidationSeverity s
            ? s switch
            {
                ValidationSeverity.Error   => "✖",  // ✖
                ValidationSeverity.Warning => "⚠",  // ⚠
                _                          => "•",  // •
            }
            : "•";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps <see cref="ValidationSeverity"/> to a brush.</summary>
public sealed class ValidationSeverityBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ErrorBrush   = new(Color.FromRgb(0xD0, 0x33, 0x33));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0xC8, 0x8A, 0x00));
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(0x55, 0x55, 0x55));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ValidationSeverity s
            ? s switch
            {
                ValidationSeverity.Error   => ErrorBrush,
                ValidationSeverity.Warning => WarningBrush,
                _                          => DefaultBrush,
            }
            : DefaultBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace D4LootBench.App.Converters;

/// <summary>Returns <see cref="Visibility.Visible"/> when the bound enum value's <c>ToString()</c> equals
/// the <c>ConverterParameter</c> string, otherwise <see cref="Visibility.Collapsed"/>. Used to show exactly
/// one wizard step panel at a time.</summary>
public sealed class EnumEqualsVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is string expected &&
        string.Equals(value.ToString(), expected, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

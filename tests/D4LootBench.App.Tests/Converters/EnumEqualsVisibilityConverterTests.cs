using System.Globalization;
using System.Windows;
using D4LootBench.App.Converters;
using D4LootBench.App.ViewModels.Progression;
using Shouldly;

namespace D4LootBench.App.Tests.Converters;

public sealed class EnumEqualsVisibilityConverterTests
{
    private static readonly EnumEqualsVisibilityConverter Converter = new();

    [Fact]
    public void Convert_matching_enum_returns_visible()
    {
        Converter.Convert(ProgressionStep.Review, typeof(Visibility), "Review", CultureInfo.InvariantCulture)
            .ShouldBe(Visibility.Visible);
    }

    [Fact]
    public void Convert_non_matching_enum_returns_collapsed()
    {
        Converter.Convert(ProgressionStep.ReadGear, typeof(Visibility), "Review", CultureInfo.InvariantCulture)
            .ShouldBe(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_is_case_sensitive_ordinal()
    {
        Converter.Convert(ProgressionStep.Review, typeof(Visibility), "review", CultureInfo.InvariantCulture)
            .ShouldBe(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_null_value_returns_collapsed()
    {
        Converter.Convert(null, typeof(Visibility), "Review", CultureInfo.InvariantCulture)
            .ShouldBe(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_null_parameter_returns_collapsed()
    {
        Converter.Convert(ProgressionStep.Review, typeof(Visibility), null, CultureInfo.InvariantCulture)
            .ShouldBe(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_null_value_and_parameter_returns_collapsed()
    {
        Converter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture)
            .ShouldBe(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_non_string_parameter_returns_collapsed()
    {
        // ConverterParameter bound to a non-string (e.g. a boxed int) must not throw — just no match.
        Converter.Convert(ProgressionStep.Review, typeof(Visibility), 42, CultureInfo.InvariantCulture)
            .ShouldBe(Visibility.Collapsed);
    }

    [Fact]
    public void Convert_empty_string_parameter_returns_collapsed()
    {
        Converter.Convert(ProgressionStep.Review, typeof(Visibility), string.Empty, CultureInfo.InvariantCulture)
            .ShouldBe(Visibility.Collapsed);
    }

    [Theory]
    [InlineData(true, "True", Visibility.Visible)]
    [InlineData(false, "True", Visibility.Collapsed)]
    [InlineData(false, "False", Visibility.Visible)]
    [InlineData(true, "False", Visibility.Collapsed)]
    [InlineData(true, "true", Visibility.Collapsed)] // ordinal, not OrdinalIgnoreCase
    public void Convert_bool_value_against_string_parameter(bool value, string parameter, Visibility expected)
    {
        Converter.Convert(value, typeof(Visibility), parameter, CultureInfo.InvariantCulture)
            .ShouldBe(expected);
    }

    [Fact]
    public void Convert_non_enum_reference_type_uses_ToString()
    {
        // Any object works, not just enums — the converter is generic over value.ToString().
        Converter.Convert("Review", typeof(Visibility), "Review", CultureInfo.InvariantCulture)
            .ShouldBe(Visibility.Visible);
    }

    [Fact]
    public void Convert_dependency_property_unset_value_does_not_throw_and_returns_collapsed()
    {
        // WPF may hand the converter Binding.DoNothing / DependencyProperty.UnsetValue in edge scenarios
        // (e.g. binding source temporarily disconnected). Must not crash the panel switch.
        var result = Converter.Convert(
            DependencyProperty.UnsetValue, typeof(Visibility), "Review", CultureInfo.InvariantCulture);
        result.ShouldBe(Visibility.Collapsed);
    }

    [Fact]
    public void ConvertBack_always_throws_NotSupportedException()
    {
        Should.Throw<NotSupportedException>(() =>
            Converter.ConvertBack(Visibility.Visible, typeof(ProgressionStep), "Review", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_with_all_null_arguments_still_throws_NotSupportedException()
    {
        Should.Throw<NotSupportedException>(() =>
            Converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }
}

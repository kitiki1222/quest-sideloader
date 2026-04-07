using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RookieMacOS.UI.Converters;

public sealed class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var truthy = value is bool b && b;
        var parts = (parameter?.ToString() ?? "34D399|FF333333").Split('|', 2);
        var hex = truthy ? parts[0] : (parts.Length > 1 ? parts[1] : parts[0]);
        if (!hex.StartsWith('#')) hex = "#" + hex;
        return Color.Parse(hex);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StringNullOrEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StringNotNullOrEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrEmpty(value?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ObjectNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class GreaterThanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        var left = System.Convert.ToDouble(value, culture);
        var right = System.Convert.ToDouble(parameter, culture);
        return left > right;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public static class BoolConverters
{
    public static IValueConverter IsTrue { get; } = new BoolToColorConverter();
}

public static class StringConverters
{
    public static IValueConverter IsEqual { get; } = new StringEqualsConverter();
    public static IValueConverter IsNullOrEmpty { get; } = new StringNullOrEmptyConverter();
    public static IValueConverter IsNotNullOrEmpty { get; } = new StringNotNullOrEmptyConverter();
}

public static class ObjectConverters
{
    public static IValueConverter IsNotNull { get; } = new ObjectNotNullConverter();
}

public static class ConverterValueConverters
{
    public static IValueConverter IsGreaterThan { get; } = new GreaterThanConverter();
}

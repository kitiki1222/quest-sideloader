using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using RookieMacOS.Core.Models;

namespace RookieMacOS.UI.Converters;

/// <summary>Converts DeviceStatus → SolidColorBrush for the indicator dot.</summary>
public class DeviceStatusToColorConverter : IValueConverter
{
    public static readonly DeviceStatusToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DeviceStatus s
            ? s switch
            {
                DeviceStatus.Online      => new SolidColorBrush(Color.Parse("#34D399")),
                DeviceStatus.Unauthorized=> new SolidColorBrush(Color.Parse("#FBBF24")),
                DeviceStatus.NoPermission=> new SolidColorBrush(Color.Parse("#FBBF24")),
                _                        => new SolidColorBrush(Color.Parse("#444444")),
            }
            : new SolidColorBrush(Colors.Gray);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Battery int → foreground color (green / amber / red).</summary>
public class BatteryToColorConverter : IValueConverter
{
    public static readonly BatteryToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int level || level < 0) return new SolidColorBrush(Colors.Gray);
        return level > 60
            ? new SolidColorBrush(Color.Parse("#34D399"))
            : level > 30
                ? new SolidColorBrush(Color.Parse("#FBBF24"))
                : new SolidColorBrush(Color.Parse("#F87171"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>InstallStatus → short label string.</summary>
public class InstallStatusToLabelConverter : IValueConverter
{
    public static readonly InstallStatusToLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallStatus s
            ? s switch
            {
                InstallStatus.Queued     => "",
                InstallStatus.Installing => "…",
                InstallStatus.Success    => "✓",
                InstallStatus.Failed     => "✗",
                _                        => ""
            }
            : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>InstallStatus → color brush.</summary>
public class InstallStatusToColorConverter : IValueConverter
{
    public static readonly InstallStatusToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallStatus s
            ? s switch
            {
                InstallStatus.Success => new SolidColorBrush(Color.Parse("#34D399")),
                InstallStatus.Failed  => new SolidColorBrush(Color.Parse("#F87171")),
                InstallStatus.Installing => new SolidColorBrush(Color.Parse("#78C8FF")),
                _                     => new SolidColorBrush(Colors.Transparent),
            }
            : new SolidColorBrush(Colors.Transparent);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>InstallStatus == Queued → bool (show Install button).</summary>
public class IsQueuedConverter : IValueConverter
{
    public static readonly IsQueuedConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallStatus.Queued;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Log level string → foreground brush for the level badge.</summary>
public class LogLevelToColorConverter : IValueConverter
{
    public static readonly LogLevelToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string level
            ? level switch
            {
                "ok"   => new SolidColorBrush(Color.Parse("#34D399")),
                "err"  => new SolidColorBrush(Color.Parse("#F87171")),
                "info" => new SolidColorBrush(Color.Parse("#78C8FF")),
                _      => new SolidColorBrush(Color.Parse("#44FFFFFF")),
            }
            : new SolidColorBrush(Colors.Gray);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Log level string → message text color.</summary>
public class LogLevelToTextColorConverter : IValueConverter
{
    public static readonly LogLevelToTextColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string level
            ? level switch
            {
                "ok"   => new SolidColorBrush(Color.Parse("#88D399C8")),
                "err"  => new SolidColorBrush(Color.Parse("#CCF87171")),
                "info" => new SolidColorBrush(Color.Parse("#9978C8FF")),
                _      => new SolidColorBrush(Color.Parse("#44FFFFFF")),
            }
            : new SolidColorBrush(Colors.Gray);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

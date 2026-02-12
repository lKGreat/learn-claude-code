using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MiniClaudeCode.Avalonia.Converters;

/// <summary>
/// Converts a hex color string to a SolidColorBrush.
/// </summary>
public class HexColorToBrushConverter : IValueConverter
{
    public static readonly HexColorToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                return SolidColorBrush.Parse(hex);
            }
            catch
            {
                return new SolidColorBrush(Colors.Gray);
            }
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts agent/todo status string to a color brush.
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    public static readonly StatusToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.ToLowerInvariant();
        var color = status switch
        {
            "running" or "in_progress" => "#60A5FA",
            "completed" or "success" or "done" => "#34D399",
            "failed" or "error" or "cancelled" => "#F87171",
            "pending" => "#9CA3AF",
            _ => "#9CA3AF"
        };
        return SolidColorBrush.Parse(color);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

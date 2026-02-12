using System.Globalization;
using Avalonia.Data.Converters;

namespace MiniClaudeCode.Avalonia.Converters;

/// <summary>
/// Converts a todo status to opacity (completed/cancelled items are dimmed).
/// </summary>
public class StatusToOpacityConverter : IValueConverter
{
    public static readonly StatusToOpacityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.ToLowerInvariant();
        return status switch
        {
            "completed" or "cancelled" => 0.5,
            _ => 1.0
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

using System.Globalization;
using Avalonia.Data.Converters;

namespace MiniClaudeCode.Avalonia.Converters;

/// <summary>
/// Converts boolean to opacity: true -> 1.0, false -> 0.5
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? 1.0 : 0.5;
        return 0.5;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

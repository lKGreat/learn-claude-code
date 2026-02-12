using System.Globalization;
using Avalonia.Data.Converters;

namespace MiniClaudeCode.Avalonia.Converters;

/// <summary>
/// Converts a boolean to IsVisible. Supports inversion via parameter "invert".
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolVal = value is true;
        if (parameter?.ToString()?.Equals("invert", StringComparison.OrdinalIgnoreCase) == true)
            boolVal = !boolVal;
        return boolVal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when a string is not null or empty.
/// </summary>
public class StringNotEmptyConverter : IValueConverter
{
    public static readonly StringNotEmptyConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrEmpty(value?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

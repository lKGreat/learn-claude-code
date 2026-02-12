using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MiniClaudeCode.Avalonia.Converters;

/// <summary>
/// Converts boolean to FontStyle: true -> Italic, false -> Normal.
/// Used for preview tab styling.
/// </summary>
public class BoolToFontStyleConverter : IValueConverter
{
    public static readonly BoolToFontStyleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? FontStyle.Italic : FontStyle.Normal;
        return FontStyle.Normal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

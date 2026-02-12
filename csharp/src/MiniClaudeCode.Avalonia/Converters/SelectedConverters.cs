using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MiniClaudeCode.Avalonia.Converters;

/// <summary>
/// Converts IsSelected to a background brush for option items.
/// </summary>
public class SelectedToBgConverter : IValueConverter
{
    public static readonly SelectedToBgConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true
            ? SolidColorBrush.Parse("#2A2B3D")
            : SolidColorBrush.Parse("#181825");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts IsSelected to an indicator background color.
/// </summary>
public class SelectedToIndicatorConverter : IValueConverter
{
    public static readonly SelectedToIndicatorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true
            ? SolidColorBrush.Parse("#60A5FA")
            : SolidColorBrush.Parse("Transparent");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

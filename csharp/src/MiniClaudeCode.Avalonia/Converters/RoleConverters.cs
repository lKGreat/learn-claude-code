using System.Globalization;
using Avalonia.Data.Converters;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.Converters;

/// <summary>
/// Returns true when the ChatMessageRole is Assistant (for Markdown rendering).
/// </summary>
public class RoleToMarkdownVisibilityConverter : IValueConverter
{
    public static readonly RoleToMarkdownVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ChatMessageRole role && role == ChatMessageRole.Assistant;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when the ChatMessageRole is NOT Assistant (for plain text rendering).
/// </summary>
public class RoleToTextVisibilityConverter : IValueConverter
{
    public static readonly RoleToTextVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ChatMessageRole role && role != ChatMessageRole.Assistant;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

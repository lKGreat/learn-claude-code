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

/// <summary>
/// Converts a boolean IsExpanded to a chevron character: expanded = "▼", collapsed = "▶".
/// </summary>
public class BoolToChevronConverter : IValueConverter
{
    public static readonly BoolToChevronConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "▼" : "▶";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when the message should show full content (expanded AND is assistant, or non-assistant).
/// </summary>
public class ExpandedAndAssistantConverter : IMultiValueConverter
{
    public static readonly ExpandedAndAssistantConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is bool isExpanded && values[1] is ChatMessageRole role)
        {
            // Non-assistant messages always show full content
            if (role != ChatMessageRole.Assistant) return true;
            // Assistant messages respect IsExpanded
            return isExpanded;
        }
        return true;
    }
}

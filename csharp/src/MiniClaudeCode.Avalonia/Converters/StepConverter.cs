using System.Globalization;
using Avalonia.Data.Converters;

namespace MiniClaudeCode.Avalonia.Converters;

/// <summary>
/// Converters for wizard step visibility. Each returns a static singleton.
/// </summary>
public class StepConverter : IValueConverter
{
    private readonly int _targetStep;
    private readonly bool _invert;

    private StepConverter(int targetStep, bool invert = false)
    {
        _targetStep = targetStep;
        _invert = invert;
    }

    public static readonly StepConverter Step1 = new(1);
    public static readonly StepConverter Step2 = new(2);
    public static readonly StepConverter Step3 = new(3);
    public static readonly StepConverter NotStep1 = new(1, invert: true);
    public static readonly StepConverter NotStep3 = new(3, invert: true);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int step)
        {
            var match = step == _targetStep;
            return _invert ? !match : match;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

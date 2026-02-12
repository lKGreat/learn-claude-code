using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MiniClaudeCode.Avalonia.Services;

/// <summary>
/// Simple debug logger for diagnosing EXPLORER issues.
/// Outputs to Debug window (visible in Visual Studio/Rider Output pane).
/// </summary>
public static class DebugLogger
{
    private static readonly string Prefix = "[EXPLORER]";

    [Conditional("DEBUG")]
    public static void Log(string message, [CallerMemberName] string caller = "")
    {
        Debug.WriteLine($"{Prefix} {caller}: {message}");
    }

    [Conditional("DEBUG")]
    public static void LogError(string message, Exception? ex = null, [CallerMemberName] string caller = "")
    {
        Debug.WriteLine($"{Prefix} ERROR in {caller}: {message}");
        if (ex != null)
            Debug.WriteLine($"{Prefix}   Exception: {ex}");
    }
}

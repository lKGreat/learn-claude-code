using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the tool call history panel.
/// </summary>
public partial class ToolCallPanelViewModel : ObservableObject
{
    public ObservableCollection<ToolCallItem> ToolCalls { get; } = [];

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _runningCount;

    public void Clear()
    {
        ToolCalls.Clear();
        TotalCount = 0;
        RunningCount = 0;
    }

    public void AddToolCall(ToolCallItem item)
    {
        ToolCalls.Insert(0, item);
        TotalCount = ToolCalls.Count;
        UpdateRunningCount();
    }

    /// <summary>
    /// Complete the most recent running tool call with the given function name.
    /// </summary>
    public void CompleteLatest(string functionName, string result, TimeSpan elapsed)
    {
        var tc = ToolCalls.FirstOrDefault(t => t.ToolName == functionName && t.Status == "Running");
        if (tc != null)
        {
            tc.Status = "Success";
            tc.Result = result.Length > 500 ? result[..500] + "..." : result;
            tc.Duration = FormatDuration(elapsed);
            UpdateRunningCount();
        }
    }

    /// <summary>
    /// Fail the most recent running tool call with the given function name.
    /// </summary>
    public void FailLatest(string functionName, string error, TimeSpan elapsed)
    {
        var tc = ToolCalls.FirstOrDefault(t => t.ToolName == functionName && t.Status == "Running");
        if (tc != null)
        {
            tc.Status = "Failed";
            tc.Error = error;
            tc.Duration = FormatDuration(elapsed);
            UpdateRunningCount();
        }
    }

    private void UpdateRunningCount()
    {
        RunningCount = ToolCalls.Count(tc => tc.Status == "Running");
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalSeconds < 1)
            return $"{ts.TotalMilliseconds:F0}ms";
        if (ts.TotalMinutes < 1)
            return $"{ts.TotalSeconds:F1}s";
        return $"{ts.TotalMinutes:F1}m";
    }
}

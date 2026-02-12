using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// Represents a single line in the diff view with its decoration.
/// </summary>
public class DiffLine
{
    public int? OldLineNumber { get; init; }
    public int? NewLineNumber { get; init; }
    public string Content { get; init; } = "";
    public DiffLineType Type { get; init; }

    public string LineNumberDisplay => Type switch
    {
        DiffLineType.Added => $"{"",5} {NewLineNumber,5}",
        DiffLineType.Removed => $"{OldLineNumber,5} {"",5}",
        DiffLineType.Context => $"{OldLineNumber,5} {NewLineNumber,5}",
        DiffLineType.Header => "",
        _ => ""
    };

    public string Foreground => Type switch
    {
        DiffLineType.Added => "#A6E3A1",
        DiffLineType.Removed => "#F87171",
        DiffLineType.Header => "#89B4FA",
        _ => "#CDD6F4"
    };

    public string Background => Type switch
    {
        DiffLineType.Added => "#1A2E1A",
        DiffLineType.Removed => "#2E1A1A",
        DiffLineType.Header => "#1A1A2E",
        _ => "Transparent"
    };

    public string Prefix => Type switch
    {
        DiffLineType.Added => "+",
        DiffLineType.Removed => "-",
        DiffLineType.Header => "@",
        _ => " "
    };
}

public enum DiffLineType
{
    Context,
    Added,
    Removed,
    Header
}

/// <summary>
/// ViewModel for the diff editor panel.
/// Parses unified diff format and presents it line-by-line.
/// </summary>
public partial class DiffEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _fileName = "";

    [ObservableProperty]
    private string _relativePath = "";

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private int _addedCount;

    [ObservableProperty]
    private int _removedCount;

    public ObservableCollection<DiffLine> DiffLines { get; } = [];

    public string StatsDisplay => $"+{AddedCount} -{RemovedCount}";

    /// <summary>Fired when the diff editor should be closed.</summary>
    public event Action? CloseRequested;

    /// <summary>Load and parse a unified diff string.</summary>
    public void LoadDiff(string filePath, string relativePath, string unifiedDiff)
    {
        FileName = Path.GetFileName(filePath);
        RelativePath = relativePath;
        IsVisible = true;

        DiffLines.Clear();
        AddedCount = 0;
        RemovedCount = 0;

        if (string.IsNullOrWhiteSpace(unifiedDiff))
        {
            DiffLines.Add(new DiffLine
            {
                Content = "(No changes detected)",
                Type = DiffLineType.Context
            });
            return;
        }

        ParseUnifiedDiff(unifiedDiff);
        OnPropertyChanged(nameof(StatsDisplay));
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        DiffLines.Clear();
        CloseRequested?.Invoke();
    }

    private void ParseUnifiedDiff(string diff)
    {
        int oldLine = 0, newLine = 0;

        foreach (var rawLine in diff.Split('\n'))
        {
            if (rawLine.StartsWith("diff --git") || rawLine.StartsWith("index ") ||
                rawLine.StartsWith("---") || rawLine.StartsWith("+++"))
            {
                // Skip file header lines
                continue;
            }

            if (rawLine.StartsWith("@@"))
            {
                // Parse hunk header: @@ -oldStart,oldCount +newStart,newCount @@
                var parts = rawLine.Split(' ');
                if (parts.Length >= 3)
                {
                    var oldPart = parts[1]; // -oldStart,oldCount
                    var newPart = parts[2]; // +newStart,newCount

                    if (oldPart.StartsWith('-'))
                    {
                        var nums = oldPart[1..].Split(',');
                        if (int.TryParse(nums[0], out var os)) oldLine = os;
                    }
                    if (newPart.StartsWith('+'))
                    {
                        var nums = newPart[1..].Split(',');
                        if (int.TryParse(nums[0], out var ns)) newLine = ns;
                    }
                }

                DiffLines.Add(new DiffLine
                {
                    Content = rawLine,
                    Type = DiffLineType.Header
                });
                continue;
            }

            if (rawLine.StartsWith('+'))
            {
                AddedCount++;
                DiffLines.Add(new DiffLine
                {
                    NewLineNumber = newLine,
                    Content = rawLine.Length > 1 ? rawLine[1..] : "",
                    Type = DiffLineType.Added
                });
                newLine++;
            }
            else if (rawLine.StartsWith('-'))
            {
                RemovedCount++;
                DiffLines.Add(new DiffLine
                {
                    OldLineNumber = oldLine,
                    Content = rawLine.Length > 1 ? rawLine[1..] : "",
                    Type = DiffLineType.Removed
                });
                oldLine++;
            }
            else if (rawLine.StartsWith(' '))
            {
                DiffLines.Add(new DiffLine
                {
                    OldLineNumber = oldLine,
                    NewLineNumber = newLine,
                    Content = rawLine.Length > 1 ? rawLine[1..] : "",
                    Type = DiffLineType.Context
                });
                oldLine++;
                newLine++;
            }
        }
    }
}

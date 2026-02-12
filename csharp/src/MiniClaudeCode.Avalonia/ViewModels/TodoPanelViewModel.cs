using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the todo/plan panel.
/// Parses the rendered text from TodoManager into structured items.
/// </summary>
public partial class TodoPanelViewModel : ObservableObject
{
    public ObservableCollection<TodoItem> Todos { get; } = [];

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _renderedText = "(no todos)";

    /// <summary>
    /// Update from the TodoManager's rendered text output.
    /// </summary>
    public void UpdateFromRendered(string rendered)
    {
        RenderedText = rendered;
        ParseRenderedText(rendered);
    }

    private void ParseRenderedText(string rendered)
    {
        Todos.Clear();

        if (string.IsNullOrWhiteSpace(rendered) || rendered == "No todos.")
        {
            TotalCount = 0;
            CompletedCount = 0;
            ProgressPercent = 0;
            return;
        }

        var lines = rendered.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int idx = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[x]"))
            {
                Todos.Add(new TodoItem { Id = $"todo-{idx}", Content = trimmed[3..].Trim(), Status = "completed" });
                idx++;
            }
            else if (trimmed.StartsWith("[>]"))
            {
                var content = trimmed[3..].Trim();
                // Remove the "<- activeForm" suffix if present
                var arrowIdx = content.IndexOf(" <- ", StringComparison.Ordinal);
                if (arrowIdx >= 0) content = content[..arrowIdx].Trim();
                Todos.Add(new TodoItem { Id = $"todo-{idx}", Content = content, Status = "in_progress" });
                idx++;
            }
            else if (trimmed.StartsWith("[ ]"))
            {
                Todos.Add(new TodoItem { Id = $"todo-{idx}", Content = trimmed[3..].Trim(), Status = "pending" });
                idx++;
            }
            // Skip the summary line like "(2/4 completed)"
        }

        TotalCount = Todos.Count;
        CompletedCount = Todos.Count(t => t.Status == "completed");
        ProgressPercent = TotalCount > 0 ? (double)CompletedCount / TotalCount * 100 : 0;
    }
}

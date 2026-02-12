using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the command palette overlay (Ctrl+Shift+P).
/// Supports command execution, file quick-open, and go-to-line.
/// </summary>
public partial class CommandPaletteViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _queryText = "";

    [ObservableProperty]
    private string _placeholder = "Type a command...";

    [ObservableProperty]
    private CommandItem? _selectedItem;

    public ObservableCollection<CommandItem> FilteredItems { get; } = [];

    private readonly List<CommandItem> _allCommands = [];
    private readonly List<string> _allFiles = [];

    /// <summary>Fired when a file should be opened (quick open mode).</summary>
    public event Action<string>? FileOpenRequested;

    /// <summary>Fired when go-to-line is requested.</summary>
    public event Action<int>? GoToLineRequested;

    public void RegisterCommand(CommandItem command)
    {
        _allCommands.Add(command);
    }

    public void SetFileList(IEnumerable<string> files)
    {
        _allFiles.Clear();
        _allFiles.AddRange(files);
    }

    partial void OnQueryTextChanged(string value)
    {
        UpdateFilteredItems(value);
    }

    [RelayCommand]
    private void Show()
    {
        IsVisible = true;
        QueryText = ">";
        Placeholder = "Type a command...";
        UpdateFilteredItems(QueryText);
    }

    [RelayCommand]
    private void ShowQuickOpen()
    {
        IsVisible = true;
        QueryText = "";
        Placeholder = "Search files by name...";
        UpdateFilteredItems("");
    }

    [RelayCommand]
    private void Hide()
    {
        IsVisible = false;
        QueryText = "";
    }

    [RelayCommand]
    private void ExecuteSelected()
    {
        if (SelectedItem?.Execute != null)
        {
            SelectedItem.Execute();
            Hide();
        }
    }

    [RelayCommand]
    private void SelectAndExecute(CommandItem? item)
    {
        if (item?.Execute != null)
        {
            item.Execute();
            Hide();
        }
    }

    private void UpdateFilteredItems(string query)
    {
        FilteredItems.Clear();

        if (query.StartsWith('>'))
        {
            // Command mode
            var filter = query[1..].Trim();
            var matches = _allCommands
                .Where(c => string.IsNullOrEmpty(filter) ||
                            c.DisplayText.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Category)
                .ThenBy(c => c.Label)
                .Take(50);

            foreach (var cmd in matches)
                FilteredItems.Add(cmd);
        }
        else if (query.StartsWith(':'))
        {
            // Go to line mode
            var lineStr = query[1..].Trim();
            if (int.TryParse(lineStr, out int line))
            {
                FilteredItems.Add(new CommandItem
                {
                    Id = "goto_line",
                    Label = $"Go to Line {line}",
                    Category = "Go",
                    Execute = () => GoToLineRequested?.Invoke(line)
                });
            }
        }
        else
        {
            // File quick open mode
            var filter = query.Trim();
            var matches = _allFiles
                .Where(f => string.IsNullOrEmpty(filter) ||
                            Path.GetFileName(f).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            f.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Take(50);

            foreach (var file in matches)
            {
                var filePath = file;
                FilteredItems.Add(new CommandItem
                {
                    Id = $"file_{filePath}",
                    Label = Path.GetFileName(filePath),
                    Category = Path.GetDirectoryName(filePath) ?? "",
                    Execute = () => FileOpenRequested?.Invoke(filePath)
                });
            }
        }

        SelectedItem = FilteredItems.FirstOrDefault();
    }
}

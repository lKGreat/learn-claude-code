using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// Command palette mode, matching VS Code's modes.
/// </summary>
public enum PaletteMode
{
    /// <summary>Commands (prefix '>') - Ctrl+Shift+P</summary>
    Commands,
    /// <summary>Files (no prefix) - Ctrl+P</summary>
    QuickOpen,
    /// <summary>Go to line (prefix ':') - Ctrl+G</summary>
    GoToLine,
    /// <summary>Go to symbol (prefix '@') - Ctrl+Shift+O</summary>
    GoToSymbol,
    /// <summary>Search in workspace (prefix '#')</summary>
    SearchInWorkspace
}

/// <summary>
/// ViewModel for the command palette overlay (Ctrl+Shift+P).
/// Supports command execution, file quick-open, go-to-line, and go-to-symbol.
/// Mirrors VS Code's Quick Input/QuickPick API.
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

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private PaletteMode _currentMode = PaletteMode.Commands;

    [ObservableProperty]
    private string _modeIndicator = "";

    public ObservableCollection<CommandItem> FilteredItems { get; } = [];

    private readonly List<CommandItem> _allCommands = [];
    private readonly List<string> _allFiles = [];
    private readonly List<SymbolItem> _allSymbols = [];

    /// <summary>Fired when a file should be opened (quick open mode).</summary>
    public event Action<string>? FileOpenRequested;

    /// <summary>Fired when go-to-line is requested.</summary>
    public event Action<int>? GoToLineRequested;

    /// <summary>Fired when go-to-symbol is requested.</summary>
    public event Action<string>? GoToSymbolRequested;

    public void RegisterCommand(CommandItem command)
    {
        _allCommands.Add(command);
    }

    public void RegisterCommands(IEnumerable<CommandItem> commands)
    {
        _allCommands.AddRange(commands);
    }

    public void SetFileList(IEnumerable<string> files)
    {
        _allFiles.Clear();
        _allFiles.AddRange(files);
    }

    public void SetSymbols(IEnumerable<SymbolItem> symbols)
    {
        _allSymbols.Clear();
        _allSymbols.AddRange(symbols);
    }

    partial void OnQueryTextChanged(string value)
    {
        UpdateFilteredItems(value);
    }

    /// <summary>Show command palette (Ctrl+Shift+P).</summary>
    [RelayCommand]
    private void Show()
    {
        IsVisible = true;
        CurrentMode = PaletteMode.Commands;
        QueryText = ">";
        Placeholder = "Type a command...";
        ModeIndicator = ">";
        UpdateFilteredItems(QueryText);
    }

    /// <summary>Show quick file open (Ctrl+P).</summary>
    [RelayCommand]
    private void ShowQuickOpen()
    {
        IsVisible = true;
        CurrentMode = PaletteMode.QuickOpen;
        QueryText = "";
        Placeholder = "Search files by name (prefix > for commands, : for line, @ for symbol)";
        ModeIndicator = "";
        UpdateFilteredItems("");
    }

    /// <summary>Show go-to-line dialog (Ctrl+G).</summary>
    [RelayCommand]
    private void ShowGoToLine()
    {
        IsVisible = true;
        CurrentMode = PaletteMode.GoToLine;
        QueryText = ":";
        Placeholder = "Type a line number...";
        ModeIndicator = ":";
        UpdateFilteredItems(":");
    }

    /// <summary>Show go-to-symbol dialog (Ctrl+Shift+O).</summary>
    [RelayCommand]
    private void ShowGoToSymbol()
    {
        IsVisible = true;
        CurrentMode = PaletteMode.GoToSymbol;
        QueryText = "@";
        Placeholder = "Type to search symbols...";
        ModeIndicator = "@";
        UpdateFilteredItems("@");
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

    /// <summary>Navigate up in the list.</summary>
    [RelayCommand]
    private void MoveUp()
    {
        if (FilteredItems.Count == 0) return;
        SelectedIndex = (SelectedIndex - 1 + FilteredItems.Count) % FilteredItems.Count;
        SelectedItem = FilteredItems[SelectedIndex];
    }

    /// <summary>Navigate down in the list.</summary>
    [RelayCommand]
    private void MoveDown()
    {
        if (FilteredItems.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % FilteredItems.Count;
        SelectedItem = FilteredItems[SelectedIndex];
    }

    private void UpdateFilteredItems(string query)
    {
        FilteredItems.Clear();

        if (query.StartsWith('>'))
        {
            // Command mode
            CurrentMode = PaletteMode.Commands;
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
            CurrentMode = PaletteMode.GoToLine;
            var lineStr = query[1..].Trim();
            if (int.TryParse(lineStr, out int line) && line > 0)
            {
                FilteredItems.Add(new CommandItem
                {
                    Id = "goto_line",
                    Label = $"Go to Line {line}",
                    Category = "Go",
                    Shortcut = "Enter",
                    Execute = () => GoToLineRequested?.Invoke(line)
                });
            }
            else
            {
                FilteredItems.Add(new CommandItem
                {
                    Id = "goto_line_hint",
                    Label = "Type a line number to navigate to",
                    Category = "Go to Line",
                    Execute = null
                });
            }
        }
        else if (query.StartsWith('@'))
        {
            // Go to symbol mode
            CurrentMode = PaletteMode.GoToSymbol;
            var filter = query[1..].Trim();
            var matches = _allSymbols
                .Where(s => string.IsNullOrEmpty(filter) ||
                            s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Kind)
                .ThenBy(s => s.Name)
                .Take(50);

            foreach (var sym in matches)
            {
                var symbolName = sym.Name;
                FilteredItems.Add(new CommandItem
                {
                    Id = $"symbol_{sym.Name}",
                    Label = $"{sym.KindIcon} {sym.Name}",
                    Category = sym.ContainerName,
                    Shortcut = $"Line {sym.Line}",
                    Execute = () => GoToSymbolRequested?.Invoke(symbolName)
                });
            }
        }
        else if (query.StartsWith('#'))
        {
            // Search in workspace mode
            CurrentMode = PaletteMode.SearchInWorkspace;
            FilteredItems.Add(new CommandItem
            {
                Id = "search_hint",
                Label = "Type to search across workspace",
                Category = "Search",
                Execute = null
            });
        }
        else
        {
            // File quick open mode - fuzzy matching
            CurrentMode = PaletteMode.QuickOpen;
            var filter = query.Trim();

            IEnumerable<string> matches;
            if (string.IsNullOrEmpty(filter))
            {
                matches = _allFiles.Take(50);
            }
            else
            {
                // Fuzzy matching: match chars in order, score by consecutive matches
                matches = _allFiles
                    .Select(f => new { File = f, Score = FuzzyScore(Path.GetFileName(f), filter) })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Take(50)
                    .Select(x => x.File);
            }

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

        SelectedIndex = 0;
        SelectedItem = FilteredItems.FirstOrDefault();
    }

    /// <summary>
    /// Simple fuzzy scoring: matches characters in order.
    /// Higher score for consecutive and prefix matches.
    /// </summary>
    private static int FuzzyScore(string text, string pattern)
    {
        int score = 0;
        int textIndex = 0;
        int consecutive = 0;

        for (int i = 0; i < pattern.Length; i++)
        {
            bool found = false;
            while (textIndex < text.Length)
            {
                if (char.ToLowerInvariant(text[textIndex]) == char.ToLowerInvariant(pattern[i]))
                {
                    score += 1;
                    consecutive++;
                    score += consecutive; // bonus for consecutive
                    if (textIndex == 0 || text[textIndex - 1] is '/' or '\\' or '.' or '-' or '_')
                        score += 3; // bonus for word boundary match
                    textIndex++;
                    found = true;
                    break;
                }
                consecutive = 0;
                textIndex++;
            }
            if (!found) return 0; // pattern char not found
        }

        return score;
    }
}

/// <summary>
/// Represents a symbol in the document (for @ symbol navigation).
/// </summary>
public class SymbolItem
{
    public string Name { get; init; } = "";
    public SymbolKind Kind { get; init; }
    public string ContainerName { get; init; } = "";
    public int Line { get; init; }
    public int Column { get; init; }

    public string KindIcon => Kind switch
    {
        SymbolKind.Class => "\u25A0",
        SymbolKind.Method => "\u25B6",
        SymbolKind.Function => "\u0192",
        SymbolKind.Property => "\u25CB",
        SymbolKind.Field => "\u25AA",
        SymbolKind.Constructor => "\u2666",
        SymbolKind.Interface => "\u25C7",
        SymbolKind.Enum => "\u2261",
        SymbolKind.Namespace => "\u007B\u007D",
        SymbolKind.Variable => "\u03B1",
        SymbolKind.Constant => "\u03C0",
        SymbolKind.Event => "\u26A1",
        _ => "\u25C6"
    };
}

public enum SymbolKind
{
    File, Module, Namespace, Package, Class, Method, Property, Field,
    Constructor, Enum, Interface, Function, Variable, Constant,
    String, Number, Boolean, Array, Object, Key, Null, EnumMember,
    Struct, Event, Operator, TypeParameter
}

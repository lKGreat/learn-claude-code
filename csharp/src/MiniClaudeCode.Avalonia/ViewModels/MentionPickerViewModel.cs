using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniClaudeCode.Abstractions.Indexing;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the mention picker that appears when users type @ in the chat input.
/// Allows selecting files or symbols to include as context.
/// </summary>
public partial class MentionPickerViewModel : ObservableObject
{
    private ICodebaseIndex? _codebaseIndex;
    private ISymbolIndex? _symbolIndex;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private int _selectedIndex;

    public ObservableCollection<MentionItem> Items { get; } = [];

    /// <summary>
    /// The currently selected item based on SelectedIndex.
    /// </summary>
    public MentionItem? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;

    /// <summary>
    /// Fired when user confirms a mention selection.
    /// </summary>
    public event Action<MentionItem>? MentionSelected;

    /// <summary>
    /// Fired when picker is closed without selection.
    /// </summary>
    public event Action? Dismissed;

    /// <summary>
    /// Set the indexing services used for searching files and symbols.
    /// </summary>
    public void SetIndexServices(ICodebaseIndex codebaseIndex, ISymbolIndex symbolIndex)
    {
        _codebaseIndex = codebaseIndex;
        _symbolIndex = symbolIndex;
    }

    /// <summary>
    /// Show the picker and clear the query.
    /// </summary>
    public void Show()
    {
        IsVisible = true;
        Query = string.Empty;
        SelectedIndex = 0;
        Items.Clear();
    }

    /// <summary>
    /// Hide the picker.
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
        Items.Clear();
        Query = string.Empty;
        SelectedIndex = 0;
    }

    /// <summary>
    /// Update the search query and filter items.
    /// </summary>
    public async void UpdateQuery(string query)
    {
        Query = query;
        SelectedIndex = 0;

        if (_codebaseIndex == null || _symbolIndex == null)
        {
            Items.Clear();
            return;
        }

        // If query is empty, show recent/popular files
        if (string.IsNullOrWhiteSpace(query))
        {
            Items.Clear();
            return;
        }

        try
        {
            // Search both files and symbols in parallel
            var fileTask = _codebaseIndex.SearchFilesAsync(query, limit: 5);
            var symbolTask = _symbolIndex.SearchSymbolsAsync(query, limit: 5);

            await Task.WhenAll(fileTask, symbolTask);

            var files = await fileTask;
            var symbols = await symbolTask;

            Items.Clear();

            // Add file results
            foreach (var file in files)
            {
                var relativePath = GetRelativePath(file.Path);
                Items.Add(new MentionItem
                {
                    Name = System.IO.Path.GetFileName(file.Path),
                    Path = file.Path,
                    Category = "File",
                    Icon = GetFileIcon(file.Language),
                    Detail = $"{file.Language} • {relativePath}",
                    Score = file.RelevanceScore
                });
            }

            // Add symbol results
            foreach (var symbol in symbols)
            {
                var relativePath = GetRelativePath(symbol.FilePath);
                Items.Add(new MentionItem
                {
                    Name = symbol.Name,
                    Path = symbol.FullyQualifiedName,
                    Category = "Symbol",
                    Icon = GetSymbolIcon(symbol.Kind),
                    Detail = $"{symbol.Kind} • {relativePath}:{symbol.Line + 1}",
                    Score = symbol.RelevanceScore
                });
            }

            // Reset selection after updating items
            SelectedIndex = Items.Count > 0 ? 0 : -1;
        }
        catch
        {
            // Silently ignore search errors
            Items.Clear();
        }
    }

    /// <summary>
    /// Move selection to the next item.
    /// </summary>
    public void SelectNext()
    {
        if (Items.Count == 0) return;
        SelectedIndex = Math.Min(SelectedIndex + 1, Items.Count - 1);
    }

    /// <summary>
    /// Move selection to the previous item.
    /// </summary>
    public void SelectPrevious()
    {
        if (Items.Count == 0) return;
        SelectedIndex = Math.Max(SelectedIndex - 1, 0);
    }

    /// <summary>
    /// Confirm the current selection and fire the MentionSelected event.
    /// </summary>
    public void ConfirmSelection()
    {
        var item = SelectedItem;
        if (item != null)
        {
            MentionSelected?.Invoke(item);
            Hide();
        }
    }

    /// <summary>
    /// Dismiss the picker without selecting.
    /// </summary>
    public void Dismiss()
    {
        Hide();
        Dismissed?.Invoke();
    }

    private static string GetRelativePath(string path)
    {
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            var relative = System.IO.Path.GetRelativePath(currentDir, path);
            return relative.Length < path.Length ? relative : path;
        }
        catch
        {
            return path;
        }
    }

    private static string GetFileIcon(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "csharp" => "🔷",
            "typescript" or "javascript" => "📜",
            "python" => "🐍",
            "java" => "☕",
            "rust" => "🦀",
            "go" => "🔵",
            "html" => "🌐",
            "css" => "🎨",
            "json" => "📋",
            "xml" => "📄",
            "markdown" => "📝",
            _ => "📄"
        };
    }

    private static string GetSymbolIcon(MiniClaudeCode.Abstractions.Indexing.SymbolKind kind)
    {
        return kind switch
        {
            MiniClaudeCode.Abstractions.Indexing.SymbolKind.Class => "🏛",
            MiniClaudeCode.Abstractions.Indexing.SymbolKind.Interface => "🔌",
            MiniClaudeCode.Abstractions.Indexing.SymbolKind.Struct => "🧱",
            MiniClaudeCode.Abstractions.Indexing.SymbolKind.Enum => "🔢",
            MiniClaudeCode.Abstractions.Indexing.SymbolKind.Method => "⚡",
            MiniClaudeCode.Abstractions.Indexing.SymbolKind.Property => "🔧",
            MiniClaudeCode.Abstractions.Indexing.SymbolKind.Field => "📦",
            MiniClaudeCode.Abstractions.Indexing.SymbolKind.Constructor => "🏗",
            MiniClaudeCode.Abstractions.Indexing.SymbolKind.Event => "📡",
            MiniClaudeCode.Abstractions.Indexing.SymbolKind.Namespace => "📁",
            _ => "●"
        };
    }
}

/// <summary>
/// Represents a single item in the mention picker list.
/// </summary>
public partial class MentionItem : ObservableObject
{
    /// <summary>Display name of the item.</summary>
    public required string Name { get; init; }

    /// <summary>Full path or fully qualified name.</summary>
    public required string Path { get; init; }

    /// <summary>Category: "File" or "Symbol".</summary>
    public required string Category { get; init; }

    /// <summary>Unicode icon for visual identification.</summary>
    public required string Icon { get; init; }

    /// <summary>Extra info (language, symbol kind, location).</summary>
    public string? Detail { get; init; }

    /// <summary>Relevance score for sorting.</summary>
    public double Score { get; init; }
}

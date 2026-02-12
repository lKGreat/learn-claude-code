using CommunityToolkit.Mvvm.ComponentModel;
using MiniClaudeCode.Avalonia.Editor.TextBuffer;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents an open file tab in the editor area.
/// </summary>
public partial class EditorTab : ObservableObject
{
    /// <summary>Full file path.</summary>
    public string FilePath { get; init; } = "";

    /// <summary>Display name (filename).</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>File extension for icon/language detection.</summary>
    public string Extension => Path.GetExtension(FilePath)?.ToLowerInvariant() ?? "";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private string _content = "";

    /// <summary>
    /// For large files: holds the PieceTree buffer to avoid materializing full content.
    /// When set, Content should only hold the viewport portion.
    /// </summary>
    public PieceTreeTextBuffer? TextBuffer { get; set; }

    /// <summary>Whether this tab uses a PieceTree buffer for large file handling.</summary>
    public bool IsLargeFile => TextBuffer != null;

    /// <summary>Language identifier for syntax highlighting.</summary>
    public string Language => Extension switch
    {
        ".cs" => "csharp",
        ".ts" or ".tsx" => "typescript",
        ".js" or ".jsx" => "javascript",
        ".py" => "python",
        ".json" => "json",
        ".xml" or ".axaml" or ".xaml" or ".csproj" or ".props" or ".targets" => "xml",
        ".html" or ".htm" => "html",
        ".css" or ".scss" or ".less" => "css",
        ".md" => "markdown",
        ".yaml" or ".yml" => "yaml",
        ".toml" => "toml",
        ".rs" => "rust",
        ".go" => "go",
        ".java" => "java",
        ".cpp" or ".cc" or ".cxx" or ".h" or ".hpp" => "cpp",
        ".c" => "c",
        ".sh" or ".bash" => "shell",
        ".ps1" or ".psm1" => "powershell",
        ".sql" => "sql",
        ".proto" => "protobuf",
        _ => "plaintext"
    };

    /// <summary>Icon glyph based on file type.</summary>
    public string Icon => Extension switch
    {
        ".cs" => "\U0001F7E2",       // green circle for C#
        ".ts" or ".tsx" => "\U0001F535",  // blue circle for TS
        ".js" or ".jsx" => "\U0001F7E1",  // yellow circle for JS
        ".py" => "\U0001F40D",             // snake for Python
        ".json" => "{ }",
        ".xml" or ".axaml" or ".xaml" => "\U0001F4C4",
        ".md" => "\U0001F4DD",
        ".css" or ".scss" => "\U0001F3A8",
        ".html" or ".htm" => "\U0001F310",
        _ => "\U0001F4C4"
    };

    /// <summary>Display title: filename with dirty indicator.</summary>
    public string DisplayTitle => IsDirty ? $"\u25CF {FileName}" : FileName;

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(DisplayTitle));
}

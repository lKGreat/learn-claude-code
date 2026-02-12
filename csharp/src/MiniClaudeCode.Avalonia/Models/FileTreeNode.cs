using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents a node in the file explorer tree.
/// </summary>
public partial class FileTreeNode : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public bool IsExpanded { get; set; }

    [ObservableProperty]
    private bool _isLoaded;

    public ObservableCollection<FileTreeNode> Children { get; } = [];

    /// <summary>
    /// Icon indicator based on file type.
    /// </summary>
    public string Icon => IsDirectory
        ? (IsExpanded ? "\U0001F4C2" : "\U0001F4C1")  // open/closed folder
        : GetFileIcon();

    private string GetFileIcon()
    {
        var ext = System.IO.Path.GetExtension(Name)?.ToLowerInvariant();
        return ext switch
        {
            ".cs" => "\u2699",      // gear
            ".xaml" or ".axaml" => "\U0001F3A8",  // palette
            ".json" => "\U0001F4CB",  // clipboard
            ".md" => "\U0001F4DD",   // memo
            ".csproj" or ".sln" or ".slnx" => "\U0001F4E6",  // package
            ".env" => "\U0001F510",  // lock
            ".gitignore" or ".git" => "\U0001F500",  // shuffle
            _ => "\U0001F4C4"  // page
        };
    }
}

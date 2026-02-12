using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Represents a node in the file explorer tree.
/// Maps to VSCode's IExplorerNode (Section 4.2).
/// </summary>
public partial class FileTreeNode : ObservableObject
{
    /// <summary>Unique identifier for this node (uses full path).</summary>
    public string Id => FullPath;

    /// <summary>Resource URI equivalent (alias for FullPath, matching doc naming).</summary>
    public string Resource => FullPath;

    /// <summary>Display name of the file or directory.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full file system path.</summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>Whether this node represents a directory.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>Parent node reference for tree navigation (doc 4.2).</summary>
    public FileTreeNode? Parent { get; set; }

    /// <summary>Callback for lazy loading children when expanded (wired by ExplorerService).</summary>
    public Action<FileTreeNode>? LoadChildrenCallback { get; set; }

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(Icon));
        // Trigger lazy loading when expanded and children not yet loaded
        if (value && IsDirectory && !IsLoaded)
            LoadChildrenCallback?.Invoke(this);
    }

    [ObservableProperty]
    private bool _isLoaded;

    /// <summary>Whether this node is currently selected (doc 4.2).</summary>
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _editName = string.Empty;

    public ObservableCollection<FileTreeNode> Children { get; } = [];

    /// <summary>
    /// Add a child node, setting the parent reference.
    /// </summary>
    public void AddChild(FileTreeNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    /// <summary>
    /// Insert a child node at a specific index, setting the parent reference.
    /// </summary>
    public void InsertChild(int index, FileTreeNode child)
    {
        child.Parent = this;
        Children.Insert(index, child);
    }

    /// <summary>
    /// Remove a child node and clear its parent reference.
    /// </summary>
    public bool RemoveChild(FileTreeNode child)
    {
        var removed = Children.Remove(child);
        if (removed)
            child.Parent = null;
        return removed;
    }

    /// <summary>
    /// Clear all children and reset their parent references.
    /// </summary>
    public void ClearChildren()
    {
        foreach (var child in Children)
            child.Parent = null;
        Children.Clear();
    }

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

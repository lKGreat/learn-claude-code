using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the file explorer panel showing workspace directory tree.
/// </summary>
public partial class FileExplorerViewModel : ObservableObject
{
    public ObservableCollection<FileTreeNode> RootNodes { get; } = [];

    [ObservableProperty]
    private string _workspacePath = string.Empty;

    [ObservableProperty]
    private string _workspaceName = string.Empty;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private FileTreeNode? _selectedNode;

    /// <summary>
    /// Fired when user double-clicks a file to view it.
    /// </summary>
    public event Action<string>? FileViewRequested;

    /// <summary>
    /// Directories to skip in the file explorer.
    /// </summary>
    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".vs", "__pycache__",
        ".venv", "venv", ".idea", ".cursor", ".codex", ".claude"
    };

    /// <summary>
    /// Load the workspace root into the tree.
    /// </summary>
    public void LoadWorkspace(string path)
    {
        WorkspacePath = path;
        WorkspaceName = Path.GetFileName(path) ?? path;
        RootNodes.Clear();

        if (!Directory.Exists(path)) return;

        try
        {
            var rootNode = CreateDirectoryNode(path, Path.GetFileName(path) ?? path);
            LoadChildren(rootNode);
            rootNode.IsExpanded = true;
            RootNodes.Add(rootNode);
        }
        catch
        {
            // Silently handle errors
        }
    }

    /// <summary>
    /// Load children of a directory node lazily.
    /// </summary>
    public void LoadChildren(FileTreeNode node)
    {
        if (!node.IsDirectory || node.IsLoaded) return;

        node.Children.Clear();

        try
        {
            // Add directories first
            var dirs = Directory.GetDirectories(node.FullPath)
                .Where(d => !SkipDirs.Contains(Path.GetFileName(d) ?? ""))
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);

            foreach (var dir in dirs)
            {
                var child = CreateDirectoryNode(dir, Path.GetFileName(dir) ?? dir);
                // Add a dummy child so the expander arrow shows
                child.Children.Add(new FileTreeNode { Name = "Loading...", FullPath = "", IsDirectory = false });
                node.Children.Add(child);
            }

            // Add files
            var files = Directory.GetFiles(node.FullPath)
                .Where(f => !ShouldSkipFile(f))
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                node.Children.Add(new FileTreeNode
                {
                    Name = Path.GetFileName(file) ?? file,
                    FullPath = file,
                    IsDirectory = false,
                    IsLoaded = true
                });
            }

            node.IsLoaded = true;
        }
        catch
        {
            // Permission errors, etc.
        }
    }

    [RelayCommand]
    private void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }

    [RelayCommand]
    private void ViewFile(FileTreeNode? node)
    {
        if (node is { IsDirectory: false, FullPath.Length: > 0 })
        {
            FileViewRequested?.Invoke(node.FullPath);
        }
    }

    private static FileTreeNode CreateDirectoryNode(string path, string name) => new()
    {
        Name = name,
        FullPath = path,
        IsDirectory = true,
        IsLoaded = false
    };

    private static bool ShouldSkipFile(string path)
    {
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        string[] binaryExts = [".exe", ".dll", ".pdb", ".bin", ".obj", ".o", ".so", ".dylib",
                               ".zip", ".tar", ".gz", ".7z", ".rar",
                               ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp",
                               ".pdf", ".mp3", ".mp4", ".avi", ".mov", ".wav",
                               ".woff", ".woff2", ".ttf", ".eot",
                               ".nupkg", ".snupkg"];
        return binaryExts.Contains(ext);
    }
}

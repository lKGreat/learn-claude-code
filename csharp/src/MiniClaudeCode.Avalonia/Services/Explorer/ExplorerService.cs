using System.Collections.ObjectModel;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.Services.Explorer;

/// <summary>
/// Implementation of the file tree data management service.
/// Extracts file system logic from FileExplorerViewModel, providing
/// a clean service layer between the UI coordinator and the file system.
/// Maps to VSCode's ExplorerService (Section 4.1).
/// </summary>
public class ExplorerService : IExplorerService
{
    /// <inheritdoc />
    public ObservableCollection<FileTreeNode> RootNodes { get; } = [];

    /// <inheritdoc />
    public ObservableCollection<FileTreeNode> SelectedNodes { get; } = [];

    /// <inheritdoc />
    public FileTreeNode? FocusedNode { get; set; }

    /// <inheritdoc />
    public IReadOnlySet<string> ExpandedNodeIds => _expandedNodeIds;

    /// <inheritdoc />
    public string WorkspacePath { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string WorkspaceName { get; private set; } = string.Empty;

    /// <inheritdoc />
    public event Action<ExplorerEvent>? EventFired;

    private readonly HashSet<string> _expandedNodeIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Directories to skip in the file explorer.
    /// </summary>
    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".vs", "__pycache__",
        ".venv", "venv", ".idea", ".cursor", ".codex", ".claude"
    };

    /// <summary>
    /// Binary file extensions to exclude.
    /// </summary>
    private static readonly HashSet<string> BinaryExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".bin", ".obj", ".o", ".so", ".dylib",
        ".zip", ".tar", ".gz", ".7z", ".rar",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp",
        ".pdf", ".mp3", ".mp4", ".avi", ".mov", ".wav",
        ".woff", ".woff2", ".ttf", ".eot",
        ".nupkg", ".snupkg"
    };

    /// <inheritdoc />
    public void LoadWorkspace(string path)
    {
        // Capture current expansion state before reload
        var previousExpanded = ExplorerViewState.CollectExpandedIds(RootNodes);

        WorkspacePath = path;
        WorkspaceName = Path.GetFileName(path) ?? path;
        RootNodes.Clear();

        if (!Directory.Exists(path)) return;

        try
        {
            var rootNode = CreateDirectoryNode(path, Path.GetFileName(path) ?? path, parent: null);
            LoadChildren(rootNode);
            rootNode.IsExpanded = true;
            _expandedNodeIds.Add(rootNode.Id);
            RootNodes.Add(rootNode);

            // Restore previous expansion state
            if (previousExpanded.Count > 0)
            {
                RestoreExpansionRecursive(rootNode, previousExpanded);
            }
        }
        catch
        {
            // Silently handle errors
        }
    }

    /// <inheritdoc />
    public void LoadChildren(FileTreeNode node)
    {
        if (!node.IsDirectory || node.IsLoaded) return;

        node.ClearChildren();

        try
        {
            // Add directories first
            var dirs = Directory.GetDirectories(node.FullPath)
                .Where(d => !SkipDirs.Contains(Path.GetFileName(d) ?? ""))
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);

            foreach (var dir in dirs)
            {
                var child = CreateDirectoryNode(dir, Path.GetFileName(dir) ?? dir, parent: node);
                // Add a dummy child so the expander arrow shows
                child.AddChild(new FileTreeNode
                {
                    Name = "Loading...",
                    FullPath = "",
                    IsDirectory = false
                });
                node.AddChild(child);
            }

            // Add files
            var files = Directory.GetFiles(node.FullPath)
                .Where(f => !ShouldSkipFile(f))
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                node.AddChild(new FileTreeNode
                {
                    Name = Path.GetFileName(file) ?? file,
                    FullPath = file,
                    IsDirectory = false,
                    IsLoaded = true,
                    Parent = node
                });
            }

            node.IsLoaded = true;
        }
        catch
        {
            // Permission errors, etc.
        }
    }

    /// <inheritdoc />
    public void SetSelectedNodes(IEnumerable<FileTreeNode> nodes)
    {
        // Clear previous selection
        foreach (var n in SelectedNodes)
            n.IsSelected = false;

        SelectedNodes.Clear();

        foreach (var node in nodes)
        {
            node.IsSelected = true;
            SelectedNodes.Add(node);
        }
    }

    /// <inheritdoc />
    public void ToggleNodeExpansion(FileTreeNode node)
    {
        if (!node.IsDirectory) return;

        node.IsExpanded = !node.IsExpanded;

        if (node.IsExpanded)
        {
            if (!node.IsLoaded)
                LoadChildren(node);
            _expandedNodeIds.Add(node.Id);
            EventFired?.Invoke(new ExplorerEvent(ExplorerEventType.NodeExpand, node));
        }
        else
        {
            _expandedNodeIds.Remove(node.Id);
            EventFired?.Invoke(new ExplorerEvent(ExplorerEventType.NodeCollapse, node));
        }
    }

    /// <inheritdoc />
    public void RefreshNode(FileTreeNode node)
    {
        if (!node.IsDirectory) return;

        // Save which children were expanded
        var childExpanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectExpandedChildIds(node, childExpanded);

        // Reload
        node.IsLoaded = false;
        LoadChildren(node);

        // Restore child expansion
        RestoreExpansionRecursive(node, childExpanded);
    }

    /// <inheritdoc />
    public void CollapseAll()
    {
        foreach (var node in RootNodes)
            CollapseRecursive(node);
        _expandedNodeIds.Clear();
    }

    /// <inheritdoc />
    public bool MoveNode(FileTreeNode source, FileTreeNode targetParent)
    {
        if (!targetParent.IsDirectory) return false;
        if (source.FullPath == targetParent.FullPath) return false;

        // Prevent moving a parent into its own child
        var check = targetParent;
        while (check != null)
        {
            if (check.Id == source.Id) return false;
            check = check.Parent;
        }

        try
        {
            var targetPath = Path.Combine(targetParent.FullPath, source.Name);

            if (source.IsDirectory)
            {
                if (Directory.Exists(targetPath)) return false;
                Directory.Move(source.FullPath, targetPath);
            }
            else
            {
                if (File.Exists(targetPath)) return false;
                File.Move(source.FullPath, targetPath);
            }

            EventFired?.Invoke(new ExplorerEvent(ExplorerEventType.NodeDrop, source, targetParent));

            // Refresh both source parent and target parent
            if (source.Parent != null)
                RefreshNode(source.Parent);
            RefreshNode(targetParent);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public FileStat? GetFileStat(FileTreeNode node)
    {
        try
        {
            if (node.IsDirectory)
            {
                var di = new DirectoryInfo(node.FullPath);
                return di.Exists ? FileStat.FromDirectoryInfo(di) : null;
            }
            else
            {
                var fi = new FileInfo(node.FullPath);
                return fi.Exists ? FileStat.FromFileInfo(fi) : null;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public ExplorerViewState CaptureViewState()
    {
        return new ExplorerViewState
        {
            RootNodes = RootNodes.ToList(),
            SelectedNodes = SelectedNodes.ToList(),
            ExpandedNodeIds = new HashSet<string>(_expandedNodeIds, StringComparer.OrdinalIgnoreCase),
            FocusedNode = FocusedNode
        };
    }

    /// <inheritdoc />
    public void RestoreViewState(ExplorerViewState state)
    {
        // Restore expansion
        _expandedNodeIds.Clear();
        foreach (var id in state.ExpandedNodeIds)
            _expandedNodeIds.Add(id);

        ExplorerViewState.RestoreExpandedState(RootNodes, _expandedNodeIds);

        // Restore focus
        FocusedNode = state.FocusedNode;
    }

    /// <summary>
    /// Fire an explorer event externally (used by ViewModel for UI-originated events).
    /// </summary>
    public void FireEvent(ExplorerEvent evt)
    {
        EventFired?.Invoke(evt);
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private FileTreeNode CreateDirectoryNode(string path, string name, FileTreeNode? parent) => new()
    {
        Name = name,
        FullPath = path,
        IsDirectory = true,
        IsLoaded = false,
        Parent = parent,
        LoadChildrenCallback = LoadChildren  // Wire lazy loading on expansion
    };

    private static bool ShouldSkipFile(string path)
    {
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        return ext != null && BinaryExts.Contains(ext);
    }

    private static void CollapseRecursive(FileTreeNode node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
            CollapseRecursive(child);
    }

    private static void CollectExpandedChildIds(FileTreeNode node, HashSet<string> ids)
    {
        foreach (var child in node.Children)
        {
            if (child.IsDirectory && child.IsExpanded)
            {
                ids.Add(child.Id);
                CollectExpandedChildIds(child, ids);
            }
        }
    }

    private void RestoreExpansionRecursive(FileTreeNode node, HashSet<string> expandedIds)
    {
        foreach (var child in node.Children)
        {
            if (child.IsDirectory && expandedIds.Contains(child.Id))
            {
                if (!child.IsLoaded)
                    LoadChildren(child);
                child.IsExpanded = true;
                _expandedNodeIds.Add(child.Id);
                RestoreExpansionRecursive(child, expandedIds);
            }
        }
    }
}

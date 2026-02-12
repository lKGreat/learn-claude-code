using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Logging;
using MiniClaudeCode.Avalonia.Models;
using MiniClaudeCode.Avalonia.Services.Explorer;
using MiniClaudeCode.Avalonia.Services;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the file explorer panel showing workspace directory tree.
/// Acts as a thin UI coordinator that delegates to IExplorerService (doc 4.1).
/// </summary>
public partial class FileExplorerViewModel : ObservableObject
{
    private IExplorerService? _explorerService;

    /// <summary>Root nodes of the file tree (bound to TreeView).</summary>
    public ObservableCollection<FileTreeNode> RootNodes => _explorerService?.RootNodes ?? _fallbackRootNodes;
    private readonly ObservableCollection<FileTreeNode> _fallbackRootNodes = [];

    /// <summary>Selected nodes for multi-selection support (doc 4.2).</summary>
    public ObservableCollection<FileTreeNode> SelectedNodes => _explorerService?.SelectedNodes ?? _fallbackSelectedNodes;
    private readonly ObservableCollection<FileTreeNode> _fallbackSelectedNodes = [];

    /// <summary>File operations (create, rename, delete) sub-ViewModel.</summary>
    public FileOperationsViewModel FileOperations { get; } = new();

    [ObservableProperty]
    private string _workspacePath = string.Empty;

    [ObservableProperty]
    private string _workspaceName = string.Empty;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private FileTreeNode? _selectedNode;

    /// <summary>
    /// Fired when user double-clicks a file to view it (permanent open).
    /// </summary>
    public event Action<string>? FileViewRequested;

    /// <summary>
    /// Fired when user single-clicks a file to preview it (preview mode).
    /// </summary>
    public event Action<string>? FilePreviewRequested;

    /// <summary>
    /// Set the explorer service instance (called from MainWindowViewModel).
    /// </summary>
    public void SetExplorerService(IExplorerService service)
    {
        _explorerService = service;

        // Subscribe to explorer events
        _explorerService.EventFired += OnExplorerEvent;

        // Notify UI that RootNodes/SelectedNodes collections changed
        OnPropertyChanged(nameof(RootNodes));
        OnPropertyChanged(nameof(SelectedNodes));
    }

    /// <summary>
    /// Handle explorer events from the service layer.
    /// </summary>
    private void OnExplorerEvent(ExplorerEvent evt)
    {
        switch (evt.Type)
        {
            case ExplorerEventType.NodeClick:
                SelectedNode = evt.Node;
                break;

            case ExplorerEventType.NodeDoubleClick:
                if (!evt.Node.IsDirectory && evt.Node.FullPath.Length > 0)
                    FileViewRequested?.Invoke(evt.Node.FullPath);
                break;

            case ExplorerEventType.NodeExpand:
            case ExplorerEventType.NodeCollapse:
                // Expansion state is already handled by ExplorerService
                break;

            case ExplorerEventType.NodeDrop:
                // Refresh was already handled by ExplorerService.MoveNode
                break;
        }
    }

    /// <summary>
    /// Load the workspace root into the tree.
    /// </summary>
    public void LoadWorkspace(string path)
    {
        WorkspacePath = path;
        WorkspaceName = Path.GetFileName(path) ?? path;
        FileOperations.SetWorkspace(path);

        if (_explorerService != null)
        {
            _explorerService.LoadWorkspace(path);
        }
    }

    /// <summary>
    /// Load children of a directory node lazily.
    /// </summary>
    public void LoadChildren(FileTreeNode node)
    {
        _explorerService?.LoadChildren(node);
    }

    [RelayCommand]
    private void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }

    [RelayCommand]
    private void Refresh()
    {
        if (!string.IsNullOrEmpty(WorkspacePath))
            LoadWorkspace(WorkspacePath);
    }

    [RelayCommand]
    private void CollapseAll()
    {
        _explorerService?.CollapseAll();
    }

    [RelayCommand]
    private void ViewFile(FileTreeNode? node)
    {
        if (node is { IsDirectory: false, FullPath.Length: > 0 })
        {
            DebugLogger.Log($"ViewFile: {node.FullPath}");
            _explorerService?.FireEvent(new ExplorerEvent(ExplorerEventType.NodeDoubleClick, node));
            FileViewRequested?.Invoke(node.FullPath);
        }
        else
        {
            DebugLogger.Log($"ViewFile called with invalid node (IsDir={node?.IsDirectory}, PathLen={node?.FullPath?.Length})");
        }
    }

    [RelayCommand]
    private void PreviewFile(FileTreeNode? node)
    {
        if (node is { IsDirectory: false, FullPath.Length: > 0 })
        {
            LogHelper.UI.Info("[Preview链路] FileExplorerViewModel.PreviewFile: 开始, Path={0}", node.FullPath);
            DebugLogger.Log($"PreviewFile: {node.FullPath}");
            _explorerService?.FireEvent(new ExplorerEvent(ExplorerEventType.NodeClick, node));
            LogHelper.UI.Info("[Preview链路] FileExplorerViewModel.PreviewFile: 触发 FilePreviewRequested");
            FilePreviewRequested?.Invoke(node.FullPath);
        }
        else
        {
            LogHelper.UI.Warn("[Preview链路] FileExplorerViewModel.PreviewFile: 无效节点 (IsDir={0}, PathLen={1})",
                node?.IsDirectory, node?.FullPath?.Length ?? 0);
            DebugLogger.Log($"PreviewFile called with invalid node");
        }
    }

    /// <summary>
    /// Toggle expansion of a directory node via the service.
    /// </summary>
    public void ToggleNodeExpansion(FileTreeNode node)
    {
        _explorerService?.ToggleNodeExpansion(node);
    }

    /// <summary>
    /// Set selected nodes via the service.
    /// </summary>
    public void SetSelectedNodes(IEnumerable<FileTreeNode> nodes)
    {
        _explorerService?.SetSelectedNodes(nodes);
    }

    /// <summary>
    /// Move a node to a new parent (drag & drop) via the service.
    /// </summary>
    public bool MoveNode(FileTreeNode source, FileTreeNode targetParent)
    {
        return _explorerService?.MoveNode(source, targetParent) ?? false;
    }
}

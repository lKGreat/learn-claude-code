using System.Collections.ObjectModel;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.Services.Explorer;

/// <summary>
/// Service interface for file tree data management, node state maintenance,
/// and event dispatching. Maps to VSCode's ExplorerService (Section 4.1).
/// 
/// This layer sits between the FileExplorerViewModel (UI coordinator) and
/// the file system, managing the tree data model and its state.
/// </summary>
public interface IExplorerService
{
    /// <summary>Root nodes of the file tree.</summary>
    ObservableCollection<FileTreeNode> RootNodes { get; }

    /// <summary>Currently selected nodes (supports multi-selection).</summary>
    ObservableCollection<FileTreeNode> SelectedNodes { get; }

    /// <summary>The node that currently has keyboard focus.</summary>
    FileTreeNode? FocusedNode { get; set; }

    /// <summary>Set of node IDs that are currently expanded.</summary>
    IReadOnlySet<string> ExpandedNodeIds { get; }

    /// <summary>The workspace root path.</summary>
    string WorkspacePath { get; }

    /// <summary>Workspace display name.</summary>
    string WorkspaceName { get; }

    /// <summary>
    /// Load a workspace root into the file tree.
    /// Preserves expansion state across reloads.
    /// </summary>
    void LoadWorkspace(string path);

    /// <summary>
    /// Lazily load children of a directory node.
    /// </summary>
    void LoadChildren(FileTreeNode node);

    /// <summary>
    /// Set the selected nodes (multi-select support).
    /// </summary>
    void SetSelectedNodes(IEnumerable<FileTreeNode> nodes);

    /// <summary>
    /// Toggle the expansion state of a directory node.
    /// </summary>
    void ToggleNodeExpansion(FileTreeNode node);

    /// <summary>
    /// Refresh a specific node (reload its children).
    /// </summary>
    void RefreshNode(FileTreeNode node);

    /// <summary>
    /// Collapse all expanded nodes.
    /// </summary>
    void CollapseAll();

    /// <summary>
    /// Move a node to a new parent directory (drag & drop).
    /// </summary>
    bool MoveNode(FileTreeNode source, FileTreeNode targetParent);

    /// <summary>
    /// Get the FileStat for a node.
    /// </summary>
    FileStat? GetFileStat(FileTreeNode node);

    /// <summary>
    /// Save and return the current view state for persistence.
    /// </summary>
    ExplorerViewState CaptureViewState();

    /// <summary>
    /// Restore a previously captured view state.
    /// </summary>
    void RestoreViewState(ExplorerViewState state);

    /// <summary>
    /// Fired when an explorer event occurs (click, expand, rename, drag, etc.).
    /// </summary>
    event Action<ExplorerEvent>? EventFired;

    /// <summary>
    /// Fire an explorer event (used by ViewModel for UI-originated events).
    /// </summary>
    void FireEvent(ExplorerEvent evt);
}

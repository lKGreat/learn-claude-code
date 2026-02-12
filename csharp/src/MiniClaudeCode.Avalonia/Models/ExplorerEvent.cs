namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Explorer event types matching VSCode's ExplorerEventType (Section 4.4).
/// </summary>
public enum ExplorerEventType
{
    /// <summary>Single-click to select a node.</summary>
    NodeClick,

    /// <summary>Double-click to open a file or toggle directory.</summary>
    NodeDoubleClick,

    /// <summary>Right-click context menu on a node.</summary>
    NodeContextMenu,

    /// <summary>Collapse a directory node.</summary>
    NodeCollapse,

    /// <summary>Expand a directory node.</summary>
    NodeExpand,

    /// <summary>Rename a node.</summary>
    NodeRename,

    /// <summary>Delete a node.</summary>
    NodeDelete,

    /// <summary>Begin dragging a node.</summary>
    NodeDragStart,

    /// <summary>Drop a node onto a target.</summary>
    NodeDrop
}

/// <summary>
/// Represents an event in the file explorer tree.
/// Maps to VSCode's ExplorerEvent dispatching pattern (Section 4.4).
/// </summary>
/// <param name="Type">The type of event.</param>
/// <param name="Node">The primary node involved in the event.</param>
/// <param name="TargetNode">Optional target node (e.g., drop target).</param>
public record ExplorerEvent(
    ExplorerEventType Type,
    FileTreeNode Node,
    FileTreeNode? TargetNode = null);

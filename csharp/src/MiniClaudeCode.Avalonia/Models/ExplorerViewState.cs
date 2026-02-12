namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Captures the state of the file explorer view for persistence and restoration.
/// Maps to VSCode's IExplorerViewState (Section 4.2).
/// </summary>
public class ExplorerViewState
{
    /// <summary>Root nodes of the file tree.</summary>
    public IReadOnlyList<FileTreeNode> RootNodes { get; set; } = [];

    /// <summary>Currently selected nodes (supports multi-selection).</summary>
    public IReadOnlyList<FileTreeNode> SelectedNodes { get; set; } = [];

    /// <summary>Set of node IDs (full paths) that are currently expanded.</summary>
    public HashSet<string> ExpandedNodeIds { get; set; } = [];

    /// <summary>The node that currently has keyboard focus.</summary>
    public FileTreeNode? FocusedNode { get; set; }

    /// <summary>
    /// Collect expanded node IDs from a tree by traversing all nodes.
    /// </summary>
    public static HashSet<string> CollectExpandedIds(IEnumerable<FileTreeNode> roots)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectExpandedIdsRecursive(roots, result);
        return result;
    }

    private static void CollectExpandedIdsRecursive(IEnumerable<FileTreeNode> nodes, HashSet<string> result)
    {
        foreach (var node in nodes)
        {
            if (node.IsExpanded && node.IsDirectory)
            {
                result.Add(node.Id);
            }

            if (node.Children.Count > 0)
            {
                CollectExpandedIdsRecursive(node.Children, result);
            }
        }
    }

    /// <summary>
    /// Restore expansion state on a newly loaded tree by matching node IDs.
    /// </summary>
    public static void RestoreExpandedState(IEnumerable<FileTreeNode> roots, HashSet<string> expandedIds)
    {
        RestoreExpandedStateRecursive(roots, expandedIds);
    }

    private static void RestoreExpandedStateRecursive(IEnumerable<FileTreeNode> nodes, HashSet<string> expandedIds)
    {
        foreach (var node in nodes)
        {
            if (node.IsDirectory && expandedIds.Contains(node.Id))
            {
                node.IsExpanded = true;
            }

            if (node.Children.Count > 0)
            {
                RestoreExpandedStateRecursive(node.Children, expandedIds);
            }
        }
    }
}

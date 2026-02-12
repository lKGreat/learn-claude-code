using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniClaudeCode.Avalonia.Logging;
using MiniClaudeCode.Avalonia.Models;
using MiniClaudeCode.Avalonia.ViewModels;
using MiniClaudeCode.Avalonia.Services;

namespace MiniClaudeCode.Avalonia.Views;

public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
        FileTree.KeyDown += OnTreeKeyDown;
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;
        if (sender is not TreeView tree) return;

        if (tree.SelectedItem is FileTreeNode node)
        {
            DebugLogger.Log($"Selected: {node.Name} (IsDir={node.IsDirectory}, Path={node.FullPath})");
            
            if (node.IsDirectory)
            {
                // VS Code behavior: single-click on directory expands it
                if (!node.IsExpanded)
                {
                    DebugLogger.Log($"Expanding directory: {node.Name}");
                    vm.ToggleNodeExpansion(node);
                    if (!node.IsLoaded)
                        vm.LoadChildren(node);
                }
            }
            else if (node.FullPath.Length > 0)
            {
                // Single-click on files: open in preview mode (italic tab, replaced by next preview)
                LogHelper.UI.Info("[Preview链路] FileExplorerView.OnTreeSelectionChanged: 单点文件触发预览, Path={0}", node.FullPath);
                DebugLogger.Log($"Preview file requested: {node.FullPath}");
                vm.PreviewFileCommand.Execute(node);
            }
        }
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;
        if (sender is not TreeView tree) return;

        if (tree.SelectedItem is FileTreeNode node)
        {
            DebugLogger.Log($"Double-tapped: {node.Name} (IsDir={node.IsDirectory})");
            
            if (node.IsDirectory)
            {
                vm.ToggleNodeExpansion(node);
                if (!node.IsLoaded)
                    vm.LoadChildren(node);
            }
            else
            {
                DebugLogger.Log($"Open file requested: {node.FullPath}");
                vm.ViewFileCommand.Execute(node);
            }
        }
    }

    /// <summary>
    /// Handle F2 for rename, Delete for delete.
    /// </summary>
    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;

        if (e.Key == Key.F2 && vm.SelectedNode != null)
        {
            vm.FileOperations.StartRenameCommand.Execute(vm.SelectedNode);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && vm.SelectedNode != null)
        {
            vm.FileOperations.DeleteNodeCommand.Execute(vm.SelectedNode);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handle Enter/Escape in the rename textbox.
    /// </summary>
    private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            // Sync EditName to RenameText before confirming
            if (vm.FileOperations.RenamingNode != null)
                vm.FileOperations.RenameText = vm.FileOperations.RenamingNode.EditName;
            vm.FileOperations.ConfirmRenameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.FileOperations.CancelRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handle Enter/Escape in the create new file/folder textbox.
    /// </summary>
    private void OnCreateBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            vm.FileOperations.ConfirmCreateCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.FileOperations.CancelCreateCommand.Execute(null);
            e.Handled = true;
        }
    }

    // =========================================================================
    // Drag & Drop Support (Doc Section 4.4)
    // Note: Simplified implementation using TreeView-level handlers
    // =========================================================================

    /// <summary>
    /// Track pointer press on a tree node to initiate drag.
    /// </summary>
    private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Drag & drop implementation can be enhanced in future iterations
        // For now, we rely on ExplorerService.MoveNode() which can be called
        // programmatically when drag & drop support is fully implemented
    }
}

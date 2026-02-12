using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniClaudeCode.Avalonia.Models;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// When a tree node is selected (single click):
    /// - Directory: lazy-load its children
    /// - File: open it in the editor
    /// </summary>
    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;
        if (sender is not TreeView tree) return;

        if (tree.SelectedItem is FileTreeNode node)
        {
            if (node.IsDirectory)
            {
                // Lazy load directory children on selection/expand
                if (!node.IsLoaded)
                {
                    vm.LoadChildren(node);
                }
            }
            else if (node.FullPath.Length > 0)
            {
                // Single click on a file => open it in the editor
                vm.ViewFileCommand.Execute(node);
            }
        }
    }

    /// <summary>
    /// When a tree node is double-clicked:
    /// - Directory: toggle expand and lazy-load
    /// - File: open in editor (also handled by single click, but this
    ///   ensures the file opens even if it was already selected)
    /// </summary>
    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;
        if (sender is not TreeView tree) return;

        if (tree.SelectedItem is FileTreeNode node)
        {
            if (node.IsDirectory)
            {
                // Toggle expand and lazy-load
                node.IsExpanded = !node.IsExpanded;
                if (!node.IsLoaded)
                {
                    vm.LoadChildren(node);
                }
            }
            else
            {
                // Open file in editor
                vm.ViewFileCommand.Execute(node);
            }
        }
    }
}

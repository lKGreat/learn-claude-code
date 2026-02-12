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
        FileTree.KeyDown += OnTreeKeyDown;
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;
        if (sender is not TreeView tree) return;

        if (tree.SelectedItem is FileTreeNode node)
        {
            if (node.IsDirectory)
            {
                if (!node.IsLoaded)
                    vm.LoadChildren(node);
            }
            else if (node.FullPath.Length > 0)
            {
                vm.ViewFileCommand.Execute(node);
            }
        }
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not FileExplorerViewModel vm) return;
        if (sender is not TreeView tree) return;

        if (tree.SelectedItem is FileTreeNode node)
        {
            if (node.IsDirectory)
            {
                node.IsExpanded = !node.IsExpanded;
                if (!node.IsLoaded)
                    vm.LoadChildren(node);
            }
            else
            {
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
}

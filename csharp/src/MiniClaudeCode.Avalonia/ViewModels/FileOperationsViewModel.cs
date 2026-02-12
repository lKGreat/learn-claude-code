using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for file and folder operations (create, rename, delete).
/// </summary>
public partial class FileOperationsViewModel : ObservableObject
{
    private string _workspacePath = string.Empty;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    [ObservableProperty]
    private FileTreeNode? _renamingNode;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string _newItemName = string.Empty;

    [ObservableProperty]
    private FileTreeNode? _creatingParent;

    [ObservableProperty]
    private bool _isCreatingFolder;

    public event Action<string>? FileCreated;
    public event Action? RefreshRequested;
    public event Func<string, Task<bool>>? ConfirmDeleteRequested;
    public event Action<string>? ErrorOccurred;

    private Window? _mainWindow;

    public void SetWorkspace(string path) => _workspacePath = path;

    public void SetMainWindow(Window window) => _mainWindow = window;

    private IClipboard? GetClipboard() =>
        _mainWindow != null ? TopLevel.GetTopLevel(_mainWindow)?.Clipboard : null;

    [RelayCommand]
    private void StartNewFile(FileTreeNode? parentNode)
    {
        IsCreating = true;
        IsCreatingFolder = false;
        NewItemName = string.Empty;
        CreatingParent = parentNode;
    }

    [RelayCommand]
    private void StartNewFolder(FileTreeNode? parentNode)
    {
        IsCreating = true;
        IsCreatingFolder = true;
        NewItemName = string.Empty;
        CreatingParent = parentNode;
    }

    [RelayCommand]
    private void ConfirmCreate()
    {
        if (string.IsNullOrWhiteSpace(NewItemName))
        {
            CancelCreate();
            return;
        }

        try
        {
            var parentPath = CreatingParent?.FullPath ?? _workspacePath;
            if (string.IsNullOrEmpty(parentPath))
            {
                ErrorOccurred?.Invoke("No workspace is open.");
                CancelCreate();
                return;
            }

            var newPath = Path.Combine(parentPath, NewItemName);

            if (IsCreatingFolder)
            {
                if (Directory.Exists(newPath))
                {
                    ErrorOccurred?.Invoke($"Folder '{NewItemName}' already exists.");
                    return;
                }
                Directory.CreateDirectory(newPath);
            }
            else
            {
                if (File.Exists(newPath))
                {
                    ErrorOccurred?.Invoke($"File '{NewItemName}' already exists.");
                    return;
                }
                File.Create(newPath).Dispose();
                FileCreated?.Invoke(newPath);
            }

            RefreshRequested?.Invoke();
            CancelCreate();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to create: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelCreate()
    {
        IsCreating = false;
        NewItemName = string.Empty;
        CreatingParent = null;
        IsCreatingFolder = false;
    }

    [RelayCommand]
    private void StartRename(FileTreeNode node)
    {
        IsRenaming = true;
        RenamingNode = node;
        RenameText = node.Name;
        node.IsRenaming = true;
        node.EditName = node.Name;
    }

    [RelayCommand]
    private void ConfirmRename()
    {
        if (RenamingNode == null || string.IsNullOrWhiteSpace(RenameText))
        {
            CancelRename();
            return;
        }

        try
        {
            var oldPath = RenamingNode.FullPath;
            var parentPath = Path.GetDirectoryName(oldPath);
            if (string.IsNullOrEmpty(parentPath))
            {
                ErrorOccurred?.Invoke("Cannot determine parent directory.");
                CancelRename();
                return;
            }

            var newPath = Path.Combine(parentPath, RenameText);

            if (oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
            {
                CancelRename();
                return;
            }

            if (RenamingNode.IsDirectory)
            {
                if (Directory.Exists(newPath))
                {
                    ErrorOccurred?.Invoke($"Folder '{RenameText}' already exists.");
                    return;
                }
                Directory.Move(oldPath, newPath);
            }
            else
            {
                if (File.Exists(newPath))
                {
                    ErrorOccurred?.Invoke($"File '{RenameText}' already exists.");
                    return;
                }
                File.Move(oldPath, newPath);
            }

            RefreshRequested?.Invoke();
            CancelRename();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to rename: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelRename()
    {
        if (RenamingNode != null)
        {
            RenamingNode.IsRenaming = false;
            RenamingNode.EditName = string.Empty;
        }
        IsRenaming = false;
        RenameText = string.Empty;
        RenamingNode = null;
    }

    [RelayCommand]
    private async Task DeleteNode(FileTreeNode node)
    {
        var confirmed = false;
        if (ConfirmDeleteRequested != null)
        {
            var itemType = node.IsDirectory ? "folder" : "file";
            confirmed = await ConfirmDeleteRequested.Invoke(
                $"Are you sure you want to delete the {itemType} '{node.Name}'?");
        }
        else
        {
            confirmed = true;
        }

        if (!confirmed) return;

        try
        {
            if (node.IsDirectory)
                Directory.Delete(node.FullPath, recursive: true);
            else
                File.Delete(node.FullPath);

            RefreshRequested?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to delete: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CopyPath(FileTreeNode node)
    {
        try
        {
            var clipboard = GetClipboard();
            if (clipboard != null)
                await clipboard.SetTextAsync(node.FullPath);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to copy path: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CopyRelativePath(FileTreeNode node)
    {
        try
        {
            var relativePath = string.IsNullOrEmpty(_workspacePath)
                ? node.FullPath
                : Path.GetRelativePath(_workspacePath, node.FullPath);

            var clipboard = GetClipboard();
            if (clipboard != null)
                await clipboard.SetTextAsync(relativePath);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to copy relative path: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RevealInExplorer(FileTreeNode node)
    {
        try
        {
            var path = node.FullPath;
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                ErrorOccurred?.Invoke("Path does not exist.");
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"-R \"{path}\"",
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsLinux())
            {
                var dir = node.IsDirectory ? path : Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = $"\"{dir}\"",
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to reveal in explorer: {ex.Message}");
        }
    }
}

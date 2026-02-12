using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// A single segment in the breadcrumb path (e.g., "src", "ViewModels", "MainWindowViewModel.cs").
/// </summary>
public partial class BreadcrumbSegment : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private bool _isLast;

    /// <summary>Sibling items at this level (for dropdown navigation).</summary>
    public ObservableCollection<BreadcrumbSibling> Siblings { get; } = [];
}

/// <summary>
/// A sibling item in a breadcrumb dropdown.
/// </summary>
public partial class BreadcrumbSibling : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private bool _isDirectory;
}

/// <summary>
/// ViewModel for file path breadcrumb navigation in the editor.
/// </summary>
public partial class BreadcrumbViewModel : ObservableObject
{
    public ObservableCollection<BreadcrumbSegment> Segments { get; } = [];

    [ObservableProperty]
    private bool _isVisible;

    /// <summary>Fired when a breadcrumb segment is clicked to navigate.</summary>
    public event Action<string>? NavigateRequested;

    /// <summary>
    /// Update the breadcrumb to reflect the given file path within the workspace.
    /// </summary>
    public void Update(string filePath, string workspacePath)
    {
        Segments.Clear();

        if (string.IsNullOrEmpty(filePath))
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;

        string relativePath;
        if (!string.IsNullOrEmpty(workspacePath) && filePath.StartsWith(workspacePath, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = Path.GetRelativePath(workspacePath, filePath);
        }
        else
        {
            relativePath = filePath;
        }

        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var currentPath = workspacePath;

        for (int i = 0; i < parts.Length; i++)
        {
            currentPath = Path.Combine(currentPath ?? "", parts[i]);
            var segment = new BreadcrumbSegment
            {
                Name = parts[i],
                FullPath = currentPath,
                IsLast = i == parts.Length - 1
            };

            // Populate siblings (other files/folders at same level)
            var parentDir = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(parentDir).OrderBy(d => Path.GetFileName(d)))
                    {
                        segment.Siblings.Add(new BreadcrumbSibling
                        {
                            Name = Path.GetFileName(dir),
                            FullPath = dir,
                            IsDirectory = true
                        });
                    }
                    foreach (var file in Directory.GetFiles(parentDir).OrderBy(f => Path.GetFileName(f)))
                    {
                        segment.Siblings.Add(new BreadcrumbSibling
                        {
                            Name = Path.GetFileName(file),
                            FullPath = file,
                            IsDirectory = false
                        });
                    }
                }
                catch
                {
                    // Ignore directory access errors
                }
            }

            Segments.Add(segment);
        }
    }

    [RelayCommand]
    private void NavigateTo(string path)
    {
        if (!string.IsNullOrEmpty(path))
            NavigateRequested?.Invoke(path);
    }
}

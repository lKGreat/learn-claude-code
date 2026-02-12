using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

public partial class MainWindow : Window
{
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Pass window reference to VM for dialogs (folder picker, etc.)
        if (DataContext is MainWindowViewModel vm)
        {
            vm.SetMainWindow(this);
        }

        // Sync the maximize/restore button icon with initial state
        UpdateMaxRestoreIcon();
    }

    // =========================================================================
    // Window Close Guard
    // =========================================================================

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_forceClose)
        {
            base.OnClosing(e);
            return;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            var dirtyTabs = vm.Editor.Tabs.Where(t => t.IsDirty).ToList();
            if (dirtyTabs.Count > 0)
            {
                e.Cancel = true;

                var fileList = string.Join(", ", dirtyTabs.Select(t => t.FileName));
                var result = await vm.QuestionDialog.AskSelectionAsync(
                    $"Save changes to {dirtyTabs.Count} file(s)? ({fileList})",
                    ["Save All", "Don't Save", "Cancel"]);

                switch (result)
                {
                    case "Save All":
                        foreach (var tab in dirtyTabs)
                            vm.Editor.SaveFileForTab(tab);
                        _forceClose = true;
                        Close();
                        break;
                    case "Don't Save":
                        _forceClose = true;
                        Close();
                        break;
                    // Cancel: do nothing, window stays open
                }

                return;
            }
        }

        base.OnClosing(e);
    }

    // =========================================================================
    // Keyboard Shortcut Routing
    // =========================================================================

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (vm.Keybindings.HandleKeyDown(e))
                return;
        }

        base.OnKeyDown(e);
    }

    // =========================================================================
    // Window Control Buttons
    // =========================================================================

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        UpdateMaxRestoreIcon();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Updates the maximize/restore button icon based on current window state.
    /// </summary>
    private void UpdateMaxRestoreIcon()
    {
        if (MaxRestoreIcon != null)
        {
            if (WindowState == WindowState.Maximized)
            {
                MaxRestoreIcon.Text = "\u29C9"; // ⧉ Two overlapping squares (restore icon)
                if (MaxRestoreButton != null)
                    ToolTip.SetTip(MaxRestoreButton, "Restore Down");
            }
            else
            {
                MaxRestoreIcon.Text = "\u25A1"; // □ Single square (maximize icon)
                if (MaxRestoreButton != null)
                    ToolTip.SetTip(MaxRestoreButton, "Maximize");
            }
        }
    }

    protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Update icon when window state changes (e.g., from double-click on title bar or Win+Up)
        if (change.Property == WindowStateProperty)
        {
            UpdateMaxRestoreIcon();
        }
    }

    // =========================================================================
    // About Dialog
    // =========================================================================

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        // Simple about message via a child window
        var aboutWindow = new Window
        {
            Title = "About",
            Width = 350,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new global::Avalonia.Thickness(20),
                Spacing = 8,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "MiniClaudeCode v0.3.0",
                        FontSize = 18,
                        FontWeight = global::Avalonia.Media.FontWeight.Bold,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "C# Semantic Kernel Edition",
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                        Opacity = 0.7
                    },
                    new TextBlock
                    {
                        Text = "Avalonia Cross-Platform UI Frontend",
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                        Opacity = 0.7
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                        Margin = new global::Avalonia.Thickness(0, 10, 0, 0)
                    }
                }
            }
        };
        aboutWindow.ShowDialog(this);
    }
}

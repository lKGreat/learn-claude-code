using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MiniClaudeCode.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

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

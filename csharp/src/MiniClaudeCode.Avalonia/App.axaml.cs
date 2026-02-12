using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MiniClaudeCode.Avalonia.Services;
using MiniClaudeCode.Avalonia.ViewModels;
using MiniClaudeCode.Avalonia.Views;

namespace MiniClaudeCode.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Initialize settings and theme services
        SettingsService.Instance.Load();
        ThemeService.Instance.Initialize();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };

            // Start engine initialization after window is shown
            vm.InitializeAsync(desktop.Args ?? []);
        }

        base.OnFrameworkInitializationCompleted();
    }
}

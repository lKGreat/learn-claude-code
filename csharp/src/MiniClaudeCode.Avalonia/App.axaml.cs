using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MiniClaudeCode.Avalonia.Logging;
using MiniClaudeCode.Avalonia.Services;
using MiniClaudeCode.Avalonia.ViewModels;
using MiniClaudeCode.Avalonia.Views;

namespace MiniClaudeCode.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        try
        {
            AvaloniaXamlLoader.Load(this);
            SettingsService.Instance.Load();
            ThemeService.Instance.Initialize();
            LogHelper.App.Debug("应用初始化完成");
        }
        catch (Exception ex)
        {
            LogHelper.App.Error(ex, "应用 Initialize 失败");
            throw;
        }
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

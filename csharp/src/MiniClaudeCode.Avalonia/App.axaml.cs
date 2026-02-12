using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MiniClaudeCode.Avalonia.ViewModels;
using MiniClaudeCode.Avalonia.Views;

namespace MiniClaudeCode.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
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

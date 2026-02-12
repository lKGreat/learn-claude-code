using Avalonia.Controls;
using Avalonia.Input;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape && DataContext is SettingsViewModel vm && vm.IsVisible)
        {
            vm.HideCommand.Execute(null);
            e.Handled = true;
        }
    }
}

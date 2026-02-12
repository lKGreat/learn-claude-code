using Avalonia.Controls;
using Avalonia.Input;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

public partial class FindReplaceOverlay : UserControl
{
    public FindReplaceOverlay()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not FindReplaceViewModel vm) return;

        switch (e.Key)
        {
            case Key.Escape:
                vm.CloseCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter when e.KeyModifiers == KeyModifiers.None:
                vm.FindNextCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                vm.FindPreviousCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}

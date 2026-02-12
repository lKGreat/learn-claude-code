using Avalonia.Controls;
using Avalonia.Input;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

public partial class InputView : UserControl
{
    public InputView()
    {
        InitializeComponent();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // Enter without Shift = send message
            e.Handled = true;
            if (DataContext is ChatViewModel vm)
            {
                vm.SendMessageCommand.Execute(null);
            }
        }
        else if (e.Key == Key.Up && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            // Alt+Up = history up
            e.Handled = true;
            if (DataContext is ChatViewModel vm)
            {
                vm.NavigateHistoryUp();
            }
        }
        else if (e.Key == Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            // Alt+Down = history down
            e.Handled = true;
            if (DataContext is ChatViewModel vm)
            {
                vm.NavigateHistoryDown();
            }
        }
    }
}

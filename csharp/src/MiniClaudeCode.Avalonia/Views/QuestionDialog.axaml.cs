using Avalonia.Controls;
using Avalonia.Input;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

public partial class QuestionDialog : UserControl
{
    public QuestionDialog()
    {
        InitializeComponent();
    }

    private void OnOptionClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: QuestionOption option }
            && DataContext is QuestionDialogViewModel vm)
        {
            vm.SelectOption(option);
        }
    }
}

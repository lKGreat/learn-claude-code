using Avalonia.Controls;
using Avalonia.Input;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

public partial class TerminalView : UserControl
{
    private TerminalViewModel? _vm;

    public TerminalView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
            _vm.OutputChanged -= ScrollToBottom;

        if (DataContext is TerminalViewModel vm)
        {
            _vm = vm;
            vm.OutputChanged += ScrollToBottom;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Enter && _vm != null)
        {
            _vm.SendCommandCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ScrollToBottom()
    {
        var scroller = this.FindControl<ScrollViewer>("TerminalScroller");
        scroller?.ScrollToEnd();
    }
}

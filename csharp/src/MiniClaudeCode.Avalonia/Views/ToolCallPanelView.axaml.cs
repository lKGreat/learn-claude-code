using Avalonia.Controls;
using Avalonia.Input;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.Views;

public partial class ToolCallPanelView : UserControl
{
    public ToolCallPanelView()
    {
        InitializeComponent();
    }

    private void OnToolCallClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ToolCallItem item })
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }
}

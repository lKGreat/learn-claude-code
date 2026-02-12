using Avalonia.Controls;
using Avalonia.Input;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.Views;

public partial class AgentPanelView : UserControl
{
    public AgentPanelView()
    {
        InitializeComponent();
    }

    private void OnAgentClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: AgentItem agent })
        {
            agent.IsExpanded = !agent.IsExpanded;
        }
    }
}

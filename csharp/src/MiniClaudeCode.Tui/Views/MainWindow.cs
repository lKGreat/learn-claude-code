using Terminal.Gui;
using MiniClaudeCode.Core.Configuration;

namespace MiniClaudeCode.Tui.Views;

/// <summary>
/// Main application window with multi-panel layout.
/// </summary>
public class MainWindow : Window
{
    public ChatView ChatView { get; }
    public InputView InputView { get; }
    public AgentPanel AgentPanel { get; }
    public TodoPanel TodoPanel { get; }
    public ToolCallPanel ToolCallPanel { get; }

    private readonly EngineContext _engine;

    public MainWindow(EngineContext engine)
    {
        _engine = engine;
        Title = $"MiniClaudeCode v0.2.0 - {engine.ActiveConfig.DisplayName}";

        // Left side: Chat + Input
        ChatView = new ChatView
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(70),
            Height = Dim.Fill(6),
        };

        InputView = new InputView
        {
            X = 0,
            Y = Pos.Bottom(ChatView),
            Width = Dim.Percent(70),
            Height = 5,
        };

        // Right side: Agent Panel + Todo Panel + ToolCall Panel
        AgentPanel = new AgentPanel
        {
            X = Pos.Right(ChatView),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(40),
        };

        TodoPanel = new TodoPanel
        {
            X = Pos.Right(ChatView),
            Y = Pos.Bottom(AgentPanel),
            Width = Dim.Fill(),
            Height = Dim.Percent(30),
        };

        ToolCallPanel = new ToolCallPanel
        {
            X = Pos.Right(InputView),
            Y = Pos.Bottom(TodoPanel),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        Add(ChatView, InputView, AgentPanel, TodoPanel, ToolCallPanel);

        // Bubble InputSubmitted from InputView
        InputView.InputSubmitted += text => InputSubmitted?.Invoke(text);

        // Focus input by default
        InputView.FocusInput();
    }

    /// <summary>
    /// Fired when user submits input from the InputView.
    /// </summary>
    public event Action<string>? InputSubmitted;

    /// <summary>
    /// Create the menu bar.
    /// </summary>
    public MenuBar CreateMenuBar(
        Action onNew,
        Action onClear,
        Action onStatus,
        Action onExit)
    {
        return new MenuBar
        {
            Menus =
            [
                new MenuBarItem("_File",
                [
                    new MenuItem("_New Conversation", "F5", onNew, shortcutKey: Key.F5),
                    new MenuItem("_Clear Screen", "F6", onClear, shortcutKey: Key.F6),
                    new MenuItem("E_xit", "F10", onExit, shortcutKey: Key.F10),
                ]),
                new MenuBarItem("_View",
                [
                    new MenuItem("_Status", "F4", onStatus, shortcutKey: Key.F4),
                ]),
                new MenuBarItem("_Help",
                [
                    new MenuItem("_About", "", () =>
                    {
                        MessageBox.Query("About", "MiniClaudeCode v0.2.0\nC# Semantic Kernel Edition\nTerminal.Gui TUI Frontend", "OK");
                    }),
                ]),
            ]
        };
    }

    /// <summary>
    /// Update the window title with current turn/tool counts.
    /// </summary>
    public void UpdateTitle()
    {
        Title = $"MiniClaudeCode - {_engine.ActiveConfig.DisplayName} | Turn:{_engine.TurnCount} Tools:{_engine.TotalToolCalls}";
    }
}

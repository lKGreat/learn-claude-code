using Avalonia.Input;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.Services;

/// <summary>
/// Central keybinding registry. Maps key gestures to command handlers.
/// </summary>
public sealed class KeybindingService
{
    private readonly Dictionary<string, KeybindingEntry> _entries = new();
    private readonly Dictionary<string, Action> _handlers = new();

    /// <summary>
    /// Register a keybinding with its handler.
    /// </summary>
    public void Register(string commandId, string gesture, string label, string category, Action handler)
    {
        _entries[commandId] = new KeybindingEntry
        {
            CommandId = commandId,
            DefaultGesture = gesture,
            CurrentGesture = gesture,
            Label = label,
            Category = category
        };
        _handlers[commandId] = handler;
    }

    /// <summary>
    /// Handle a key down event. Returns true if a keybinding was matched and executed.
    /// </summary>
    public bool HandleKeyDown(KeyEventArgs e)
    {
        var gestureStr = BuildGestureString(e);
        if (string.IsNullOrEmpty(gestureStr)) return false;

        foreach (var entry in _entries.Values)
        {
            if (string.Equals(entry.CurrentGesture, gestureStr, StringComparison.OrdinalIgnoreCase)
                && _handlers.TryGetValue(entry.CommandId, out var handler))
            {
                handler();
                e.Handled = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get the display string for a command's current keybinding.
    /// </summary>
    public string? GetShortcutDisplay(string commandId)
    {
        return _entries.TryGetValue(commandId, out var entry) ? entry.CurrentGesture : null;
    }

    /// <summary>
    /// Get all registered keybindings.
    /// </summary>
    public IReadOnlyCollection<KeybindingEntry> GetAll() => _entries.Values;

    private static string BuildGestureString(KeyEventArgs e)
    {
        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");

        var key = e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return "";

        parts.Add(key switch
        {
            Key.OemTilde => "`",
            Key.OemPlus => "=",
            Key.OemMinus => "-",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            _ => key.ToString()
        });

        return string.Join("+", parts);
    }
}

using System.Text.RegularExpressions;
using Spectre.Console;

namespace MiniClaudeCode.Cli;

/// <summary>
/// Custom line editor with tab completion, history, ghost-text suggestions, and robust Ctrl+C handling.
///
/// Features:
///   - Character-by-character input via Console.ReadKey (requires TreatControlCAsInput = true)
///   - Cursor movement: Left/Right, Ctrl+Left/Right (word), Home/End, Ctrl+A/E
///   - Deletion: Backspace, Delete, Ctrl+Backspace (word), Ctrl+U (clear line)
///   - History: Up/Down arrows
///   - Tab completion for slash commands (single match auto-completes, multiple shows list)
///   - Ghost-text suggestion (dim inline text for top match, accept with Right arrow or Tab)
///   - Ctrl+C: cancels current line; double Ctrl+C with empty line to exit
///   - Escape: clear current line
///   - Graceful fallback to Console.ReadLine when input is redirected
///
/// Inspired by GNU Readline and Codex CLI's input handling.
/// </summary>
public partial class LineEditor
{
    private readonly List<string> _history = [];
    private readonly List<string> _completions = [];
    private DateTime _lastCtrlC = DateTime.MinValue;
    private static readonly TimeSpan DoubleCtrlCWindow = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Whether a double Ctrl+C exit was requested.
    /// </summary>
    public bool ExitRequested { get; private set; }

    /// <summary>
    /// Set the available tab-completions (e.g. "/help", "/exit", "/model").
    /// </summary>
    public void SetCompletions(IEnumerable<string> completions)
    {
        _completions.Clear();
        _completions.AddRange(completions);
        _completions.Sort(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Add a line to the input history (skips duplicates of last entry).
    /// </summary>
    public void AddHistory(string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            if (_history.Count == 0 || _history[^1] != line)
                _history.Add(line);
        }
    }

    /// <summary>
    /// Read a line of input with prompt, tab completion, history, and Ctrl+C handling.
    /// </summary>
    /// <param name="prompt">Spectre.Console markup string for the prompt (e.g. "[bold cyan]>> [/]").</param>
    /// <returns>
    /// The input string, or null if exit is requested (double Ctrl+C).
    /// Returns empty string "" on cancelled input (single Ctrl+C with content).
    /// </returns>
    public string? ReadLine(string prompt)
    {
        // Fallback for redirected input (pipes, CI, etc.)
        if (Console.IsInputRedirected)
        {
            AnsiConsole.Markup(prompt);
            return Console.ReadLine()?.Trim();
        }

        AnsiConsole.Markup(prompt);
        var promptLen = MeasureVisibleLength(prompt);

        var buffer = new List<char>();
        var cursorPos = 0;
        var historyIndex = _history.Count; // past the end = current (unsaved) input
        var savedInput = "";               // current input saved when navigating history

        while (true)
        {
            // --- Ghost suggestion ---
            var suggestion = GetSuggestion(buffer, cursorPos);
            if (suggestion.Length > 0)
                ShowGhostText(suggestion);

            // --- Read one key ---
            var key = Console.ReadKey(true);

            // --- Clear ghost text before processing ---
            if (suggestion.Length > 0)
                EraseGhostText(suggestion.Length);

            // ===== Ctrl+C =====
            if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                var now = DateTime.UtcNow;
                if (buffer.Count == 0 && (now - _lastCtrlC) < DoubleCtrlCWindow)
                {
                    // Double Ctrl+C on empty line → exit
                    ExitRequested = true;
                    Console.WriteLine();
                    return null;
                }
                _lastCtrlC = now;

                Console.WriteLine();
                if (buffer.Count > 0)
                {
                    // Cancel current input, redraw empty prompt
                    buffer.Clear();
                    cursorPos = 0;
                }
                else
                {
                    AnsiConsole.MarkupLine("[dim]Press Ctrl+C again to exit, or type /exit[/]");
                }
                AnsiConsole.Markup(prompt);
                continue;
            }

            // Reset double-Ctrl+C timer on any other key
            _lastCtrlC = DateTime.MinValue;

            switch (key.Key)
            {
                // ===== Enter =====
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    var result = new string(buffer.ToArray());
                    if (!string.IsNullOrWhiteSpace(result))
                        AddHistory(result);
                    return result;

                // ===== Backspace =====
                case ConsoleKey.Backspace:
                    if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        // Ctrl+Backspace: delete word
                        if (cursorPos > 0)
                        {
                            var boundary = FindWordBoundaryLeft(buffer, cursorPos);
                            buffer.RemoveRange(boundary, cursorPos - boundary);
                            cursorPos = boundary;
                            Redraw(prompt, promptLen, buffer, cursorPos);
                        }
                    }
                    else if (cursorPos > 0)
                    {
                        buffer.RemoveAt(cursorPos - 1);
                        cursorPos--;
                        Redraw(prompt, promptLen, buffer, cursorPos);
                    }
                    break;

                // ===== Delete =====
                case ConsoleKey.Delete:
                    if (cursorPos < buffer.Count)
                    {
                        buffer.RemoveAt(cursorPos);
                        Redraw(prompt, promptLen, buffer, cursorPos);
                    }
                    break;

                // ===== Left Arrow =====
                case ConsoleKey.LeftArrow:
                    if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                        cursorPos = FindWordBoundaryLeft(buffer, cursorPos);
                    else if (cursorPos > 0)
                        cursorPos--;
                    SetCursorX(promptLen + cursorPos);
                    break;

                // ===== Right Arrow =====
                case ConsoleKey.RightArrow:
                    if (suggestion.Length > 0 && cursorPos == buffer.Count)
                    {
                        // Accept ghost suggestion
                        buffer.AddRange(suggestion);
                        cursorPos = buffer.Count;
                        Redraw(prompt, promptLen, buffer, cursorPos);
                    }
                    else if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        cursorPos = FindWordBoundaryRight(buffer, cursorPos);
                        SetCursorX(promptLen + cursorPos);
                    }
                    else if (cursorPos < buffer.Count)
                    {
                        cursorPos++;
                        SetCursorX(promptLen + cursorPos);
                    }
                    break;

                // ===== Home / End =====
                case ConsoleKey.Home:
                    cursorPos = 0;
                    SetCursorX(promptLen);
                    break;

                case ConsoleKey.End:
                    cursorPos = buffer.Count;
                    SetCursorX(promptLen + cursorPos);
                    break;

                // ===== Up / Down (history) =====
                case ConsoleKey.UpArrow:
                    if (historyIndex > 0)
                    {
                        if (historyIndex == _history.Count)
                            savedInput = new string(buffer.ToArray());
                        historyIndex--;
                        SetBuffer(buffer, _history[historyIndex], out cursorPos);
                        Redraw(prompt, promptLen, buffer, cursorPos);
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (historyIndex < _history.Count)
                    {
                        historyIndex++;
                        var text = historyIndex < _history.Count ? _history[historyIndex] : savedInput;
                        SetBuffer(buffer, text, out cursorPos);
                        Redraw(prompt, promptLen, buffer, cursorPos);
                    }
                    break;

                // ===== Tab (completion) =====
                case ConsoleKey.Tab:
                    HandleTab(buffer, ref cursorPos, prompt, promptLen);
                    break;

                // ===== Escape (clear line) =====
                case ConsoleKey.Escape:
                    buffer.Clear();
                    cursorPos = 0;
                    Redraw(prompt, promptLen, buffer, cursorPos);
                    break;

                // ===== Other keys =====
                default:
                    // Ctrl+U: clear entire line
                    if (key.Key == ConsoleKey.U && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        buffer.Clear();
                        cursorPos = 0;
                        Redraw(prompt, promptLen, buffer, cursorPos);
                        break;
                    }

                    // Ctrl+A: move to start
                    if (key.Key == ConsoleKey.A && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        cursorPos = 0;
                        SetCursorX(promptLen);
                        break;
                    }

                    // Ctrl+E: move to end
                    if (key.Key == ConsoleKey.E && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        cursorPos = buffer.Count;
                        SetCursorX(promptLen + cursorPos);
                        break;
                    }

                    // Ctrl+W: delete word backward (same as Ctrl+Backspace)
                    if (key.Key == ConsoleKey.W && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        if (cursorPos > 0)
                        {
                            var boundary = FindWordBoundaryLeft(buffer, cursorPos);
                            buffer.RemoveRange(boundary, cursorPos - boundary);
                            cursorPos = boundary;
                            Redraw(prompt, promptLen, buffer, cursorPos);
                        }
                        break;
                    }

                    // Regular printable character
                    if (!char.IsControl(key.KeyChar) && key.KeyChar != '\0')
                    {
                        buffer.Insert(cursorPos, key.KeyChar);
                        cursorPos++;

                        if (cursorPos == buffer.Count)
                        {
                            // Cursor at end: just append (fast path)
                            Console.Write(key.KeyChar);
                        }
                        else
                        {
                            // Cursor in middle: full redraw
                            Redraw(prompt, promptLen, buffer, cursorPos);
                        }
                    }
                    break;
            }
        }
    }

    // ─────────────────────────── Ghost-text suggestions ───────────────────────────

    /// <summary>
    /// Get the ghost-text suggestion for the current buffer.
    /// Only suggests when cursor is at end and input starts with '/'.
    /// Returns the suffix to append (empty string if no suggestion).
    /// </summary>
    private string GetSuggestion(List<char> buffer, int cursorPos)
    {
        // Only suggest when cursor is at the end
        if (cursorPos != buffer.Count || buffer.Count == 0)
            return "";

        var text = new string(buffer.ToArray());

        // Slash command completion
        if (text.StartsWith('/'))
        {
            var matches = _completions
                .Where(c => c.StartsWith(text, StringComparison.OrdinalIgnoreCase) && c.Length > text.Length)
                .ToList();

            if (matches.Count >= 1)
                return matches[0][text.Length..];
        }

        return "";
    }

    /// <summary>
    /// Display ghost text (dim gray) after the cursor position.
    /// </summary>
    private static void ShowGhostText(string suggestion)
    {
        try
        {
            var savedLeft = Console.CursorLeft;
            var savedTop = Console.CursorTop;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(suggestion);
            Console.ResetColor();
            Console.SetCursorPosition(savedLeft, savedTop);
        }
        catch
        {
            // Ignore cursor positioning errors (terminal resize, etc.)
        }
    }

    /// <summary>
    /// Erase previously displayed ghost text.
    /// </summary>
    private static void EraseGhostText(int length)
    {
        try
        {
            var savedLeft = Console.CursorLeft;
            var savedTop = Console.CursorTop;
            Console.Write(new string(' ', length));
            Console.SetCursorPosition(savedLeft, savedTop);
        }
        catch
        {
            // Ignore
        }
    }

    // ─────────────────────────── Tab completion ───────────────────────────

    /// <summary>
    /// Handle Tab key: auto-complete slash commands.
    /// - Single match: complete and add trailing space
    /// - Multiple matches: show list and complete common prefix
    /// </summary>
    private void HandleTab(List<char> buffer, ref int cursorPos, string prompt, int promptLen)
    {
        var text = new string(buffer.ToArray());

        // Only complete slash commands
        if (!text.StartsWith('/'))
            return;

        var matches = _completions
            .Where(c => c.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            return;

        if (matches.Count == 1)
        {
            // Single match: complete it and add a trailing space
            SetBuffer(buffer, matches[0] + " ", out cursorPos);
            Redraw(prompt, promptLen, buffer, cursorPos);
        }
        else
        {
            // Multiple matches: show list, then complete common prefix
            Console.WriteLine();

            var grid = new Grid();
            grid.AddColumn(new GridColumn().PadRight(4));
            grid.AddColumn(new GridColumn());

            foreach (var match in matches)
            {
                var cmd = _completions.Contains(match) ? match : match;
                grid.AddRow($"[cyan]{Markup.Escape(cmd)}[/]", "");
            }
            AnsiConsole.Write(new Padder(grid, new Padding(2, 0, 0, 0)));

            // Complete to common prefix
            var common = FindCommonPrefix(matches);
            if (common.Length > text.Length)
                SetBuffer(buffer, common, out cursorPos);

            // Redraw prompt and buffer
            AnsiConsole.Markup(prompt);
            Console.Write(new string(buffer.ToArray()));
            SetCursorX(promptLen + cursorPos);
        }
    }

    /// <summary>
    /// Find the longest common prefix (case-insensitive) among strings.
    /// </summary>
    private static string FindCommonPrefix(List<string> strings)
    {
        if (strings.Count == 0) return "";
        var prefix = strings[0];
        for (int i = 1; i < strings.Count; i++)
        {
            var s = strings[i];
            var len = Math.Min(prefix.Length, s.Length);
            int j = 0;
            while (j < len && char.ToLowerInvariant(prefix[j]) == char.ToLowerInvariant(s[j]))
                j++;
            prefix = prefix[..j];
        }
        return prefix;
    }

    // ─────────────────────────── Line editing helpers ───────────────────────────

    /// <summary>
    /// Replace buffer contents and move cursor to end.
    /// </summary>
    private static void SetBuffer(List<char> buffer, string text, out int cursorPos)
    {
        buffer.Clear();
        buffer.AddRange(text);
        cursorPos = buffer.Count;
    }

    /// <summary>
    /// Redraw the entire line (prompt + buffer) and position the cursor.
    /// </summary>
    private static void Redraw(string prompt, int promptLen, List<char> buffer, int cursorPos)
    {
        try
        {
            var top = Console.CursorTop;
            Console.SetCursorPosition(0, top);

            // Re-render prompt (with Spectre markup colors)
            AnsiConsole.Markup(prompt);

            // Write buffer content
            var text = new string(buffer.ToArray());
            Console.Write(text);

            // Clear any leftover characters from previous longer content
            var totalUsed = promptLen + text.Length;
            var bufferWidth = GetBufferWidth();
            var clearLen = bufferWidth - totalUsed - 1;
            if (clearLen > 0)
                Console.Write(new string(' ', clearLen));

            // Position cursor
            Console.SetCursorPosition(promptLen + cursorPos, top);
        }
        catch
        {
            // Ignore cursor/positioning errors
        }
    }

    /// <summary>
    /// Set the cursor's X position on the current line.
    /// </summary>
    private static void SetCursorX(int x)
    {
        try
        {
            Console.SetCursorPosition(x, Console.CursorTop);
        }
        catch
        {
            // Ignore
        }
    }

    // ─────────────────────────── Word boundary helpers ───────────────────────────

    /// <summary>
    /// Find the start of the previous word (for Ctrl+Left / Ctrl+Backspace).
    /// </summary>
    private static int FindWordBoundaryLeft(List<char> buffer, int pos)
    {
        if (pos <= 0) return 0;
        pos--;
        // Skip whitespace
        while (pos > 0 && buffer[pos] == ' ') pos--;
        // Skip word characters
        while (pos > 0 && buffer[pos - 1] != ' ') pos--;
        return pos;
    }

    /// <summary>
    /// Find the end of the next word (for Ctrl+Right).
    /// </summary>
    private static int FindWordBoundaryRight(List<char> buffer, int pos)
    {
        if (pos >= buffer.Count) return buffer.Count;
        // Skip whitespace
        while (pos < buffer.Count && buffer[pos] == ' ') pos++;
        // Skip word characters
        while (pos < buffer.Count && buffer[pos] != ' ') pos++;
        return pos;
    }

    // ─────────────────────────── Utility ───────────────────────────

    /// <summary>
    /// Get the visible length of a Spectre.Console markup string (strips [tags]).
    /// </summary>
    private static int MeasureVisibleLength(string markup)
    {
        return MarkupTagRegex().Replace(markup, "").Length;
    }

    /// <summary>
    /// Get the console buffer width, with a safe fallback.
    /// </summary>
    private static int GetBufferWidth()
    {
        try { return Console.BufferWidth; }
        catch { return 120; }
    }

    [GeneratedRegex(@"\[/?[^\]]*\]")]
    private static partial Regex MarkupTagRegex();
}

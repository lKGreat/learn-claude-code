using System.Text.RegularExpressions;
using Spectre.Console;

namespace MiniClaudeCode.Cli;

/// <summary>
/// Renders LLM output with Spectre.Console markup.
///
/// Handles:
///   - Code blocks (``` ... ```) -> Panels with language label
///   - Inline code (`...`) -> styled markup
///   - Bold (**...**) -> [bold]
///   - Headers (# ...) -> styled lines
///   - Lists (- ...) -> preserved with color
///   - Regular text -> clean output
///
/// Color scheme:
///   User input    = Cyan
///   Assistant text = White (default)
///   Tool calls    = DarkGray (handled by ToolCallVisualizer)
///   Errors        = Red
///   System        = Yellow
/// </summary>
public static partial class OutputRenderer
{
    /// <summary>
    /// Render assistant output to the terminal.
    /// Parses basic markdown and renders with Spectre.Console formatting.
    /// </summary>
    public static void RenderAssistantMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        var lines = content.Split('\n');
        var inCodeBlock = false;
        var codeBlockLang = "";
        var codeBlockLines = new List<string>();

        foreach (var line in lines)
        {
            // Code block start/end
            if (line.TrimStart().StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    // Start of code block
                    inCodeBlock = true;
                    codeBlockLang = line.TrimStart()[3..].Trim();
                    codeBlockLines.Clear();
                }
                else
                {
                    // End of code block — render it
                    inCodeBlock = false;
                    RenderCodeBlock(codeBlockLang, string.Join('\n', codeBlockLines));
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlockLines.Add(line);
                continue;
            }

            // Headers
            if (line.StartsWith("### "))
            {
                AnsiConsole.MarkupLine($"[bold]{SafeMarkup(line[4..])}[/]");
            }
            else if (line.StartsWith("## "))
            {
                AnsiConsole.MarkupLine($"[bold cyan]{SafeMarkup(line[3..])}[/]");
            }
            else if (line.StartsWith("# "))
            {
                AnsiConsole.MarkupLine($"[bold underline cyan]{SafeMarkup(line[2..])}[/]");
            }
            // List items
            else if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
            {
                var indent = line.Length - line.TrimStart().Length;
                var bullet = new string(' ', indent) + "[cyan]\u2022[/] ";
                var text = line.TrimStart()[2..];
                AnsiConsole.MarkupLine(bullet + FormatInlineMarkdown(text));
            }
            // Numbered list items
            else if (NumberedListRegex().IsMatch(line.TrimStart()))
            {
                AnsiConsole.MarkupLine(FormatInlineMarkdown(line));
            }
            // Horizontal rule
            else if (line.Trim() is "---" or "***" or "___")
            {
                AnsiConsole.Write(new Rule().RuleStyle(new Style(Color.Grey)));
            }
            // Regular text
            else
            {
                AnsiConsole.MarkupLine(FormatInlineMarkdown(line));
            }
        }

        // If we ended inside a code block (malformed), flush it
        if (inCodeBlock && codeBlockLines.Count > 0)
        {
            RenderCodeBlock(codeBlockLang, string.Join('\n', codeBlockLines));
        }
    }

    /// <summary>
    /// Render a code block as a Spectre.Console Panel.
    /// </summary>
    private static void RenderCodeBlock(string language, string code)
    {
        var header = string.IsNullOrEmpty(language) ? " code " : $" {language} ";
        var panel = new Panel(Markup.Escape(code))
        {
            Header = new PanelHeader($"[dim]{Markup.Escape(header)}[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0),
        };
        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Format inline markdown: bold, inline code, italic.
    /// </summary>
    private static string FormatInlineMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // First, escape Spectre markup characters
        var result = SafeMarkup(text);

        // Bold: **text** -> [bold]text[/]
        result = BoldRegex().Replace(result, "[bold]$1[/]");

        // Inline code: `text` -> [yellow]text[/]
        result = InlineCodeRegex().Replace(result, "[yellow]$1[/]");

        // Italic: *text* or _text_ -> [italic]text[/]
        result = ItalicRegex().Replace(result, "[italic]$1[/]");

        return result;
    }

    /// <summary>
    /// Escape text for Spectre.Console markup ([ and ] are special).
    /// </summary>
    private static string SafeMarkup(string text)
    {
        return Markup.Escape(text);
    }

    /// <summary>
    /// Render a user prompt echo.
    /// </summary>
    public static void RenderUserPrompt(string input)
    {
        // Don't echo — the REPL prompt already shows the input
    }

    /// <summary>
    /// Render an error message.
    /// </summary>
    public static void RenderError(string message)
    {
        AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(message)}[/]");
    }

    /// <summary>
    /// Render a system/info message.
    /// </summary>
    public static void RenderSystem(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
    }

    [GeneratedRegex(@"^\d+\.\s")]
    private static partial Regex NumberedListRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"`(.+?)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"(?<!\*)\*([^*]+?)\*(?!\*)")]
    private static partial Regex ItalicRegex();
}

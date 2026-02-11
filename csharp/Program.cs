/// <summary>
/// Mini Claude Code - C# Edition
///
/// A professional coding agent CLI built with Microsoft Semantic Kernel.
/// Inspired by Claude Code and Codex CLI.
///
/// Usage:
///   MiniClaudeCode.exe                         # Double-click or run: Interactive REPL
///   dotnet run                                 # Interactive REPL
///   dotnet run -- "prompt"                     # Print mode (answer and exit)
///   dotnet run -- -p "prompt"                  # Explicit print mode
///   dotnet run -- --provider deepseek          # Override provider
///   dotnet run -- --model deepseek-chat        # Override model
///   dotnet run -- -v                           # Print version
/// </summary>

using MiniClaudeCode.Cli;

var cliArgs = CliArgs.Parse(args);
var isInteractive = cliArgs.Mode == CliMode.Interactive && string.IsNullOrEmpty(cliArgs.Prompt);

try
{
    var app = new CliApp();
    await app.RunAsync(cliArgs);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nFatal error: {ex.Message}");
    Console.ResetColor();
}

// When double-clicked (interactive mode, no args), prevent window from closing
if (isInteractive && !Console.IsInputRedirected)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("\nPress any key to exit...");
    Console.ResetColor();
    try { Console.ReadKey(true); } catch { }
}

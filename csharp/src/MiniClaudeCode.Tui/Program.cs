/// <summary>
/// MiniClaudeCode TUI - Terminal.Gui Frontend
///
/// Usage:
///   dotnet run                           Interactive TUI
///   dotnet run "prompt"                  Print mode: answer and exit
///   dotnet run -p "prompt"               Print mode (explicit)
///   dotnet run --provider deepseek       Use specific provider
///   dotnet run --model gpt-4o-mini       Use specific model
///   dotnet run -v                        Show version
/// </summary>

using MiniClaudeCode.Tui.App;

var cliArgs = CliArgs.Parse(args);

try
{
    var app = new TuiApp();
    await app.RunAsync(cliArgs);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nFatal error: {ex.Message}");
    Console.ResetColor();
}

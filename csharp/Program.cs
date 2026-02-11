/// <summary>
/// Mini Claude Code - C# Edition
///
/// A professional coding agent CLI built with Microsoft Semantic Kernel.
/// Inspired by Claude Code and Codex CLI.
///
/// Usage:
///   dotnet run                            # Interactive REPL
///   dotnet run -- "prompt"                # Print mode (answer and exit)
///   dotnet run -- -p "prompt"             # Explicit print mode
///   dotnet run -- --provider deepseek     # Override provider
///   dotnet run -- --model deepseek-chat   # Override model
///   dotnet run -- -v                      # Print version
/// </summary>

using MiniClaudeCode.Cli;

var cliArgs = CliArgs.Parse(args);
var app = new CliApp();
await app.RunAsync(cliArgs);

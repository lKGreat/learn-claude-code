namespace MiniClaudeCode.Cli.Commands;

/// <summary>
/// Interface for slash commands.
///
/// Slash commands are invoked by typing /name in the REPL.
/// Each command has a name, optional aliases, description, and an async execute method.
/// </summary>
public interface ISlashCommand
{
    /// <summary>Primary command name (without the / prefix).</summary>
    string Name { get; }

    /// <summary>Alternative names (e.g., "q" for quit).</summary>
    string[] Aliases => [];

    /// <summary>Short description shown in /help.</summary>
    string Description { get; }

    /// <summary>Execute the command.</summary>
    /// <param name="args">Arguments after the command name.</param>
    /// <param name="context">Shared CLI context.</param>
    Task ExecuteAsync(string args, CliContext context);
}

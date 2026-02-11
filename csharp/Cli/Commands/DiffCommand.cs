using System.Diagnostics;
using Spectre.Console;

namespace MiniClaudeCode.Cli.Commands;

public class DiffCommand : ISlashCommand
{
    public string Name => "diff";
    public string Description => "Show git diff of current changes";

    public async Task ExecuteAsync(string args, CliContext context)
    {
        try
        {
            // Staged changes
            var staged = await RunGitAsync("diff --cached --stat", context.WorkDir);
            // Unstaged changes
            var unstaged = await RunGitAsync("diff --stat", context.WorkDir);
            // Untracked files
            var untracked = await RunGitAsync("ls-files --others --exclude-standard", context.WorkDir);

            var hasChanges = false;

            if (!string.IsNullOrWhiteSpace(staged))
            {
                hasChanges = true;
                AnsiConsole.Write(new Panel(Markup.Escape(staged.Trim()))
                {
                    Header = new PanelHeader("[green] Staged [/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Green),
                });
            }

            if (!string.IsNullOrWhiteSpace(unstaged))
            {
                hasChanges = true;
                AnsiConsole.Write(new Panel(Markup.Escape(unstaged.Trim()))
                {
                    Header = new PanelHeader("[yellow] Unstaged [/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Yellow),
                });
            }

            if (!string.IsNullOrWhiteSpace(untracked))
            {
                hasChanges = true;
                AnsiConsole.Write(new Panel(Markup.Escape(untracked.Trim()))
                {
                    Header = new PanelHeader("[blue] Untracked [/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Blue),
                });
            }

            if (!hasChanges)
            {
                AnsiConsole.MarkupLine("[dim]No changes detected.[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Git error: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private static async Task<string> RunGitAsync(string arguments, string workDir)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)!;
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return output;
    }
}

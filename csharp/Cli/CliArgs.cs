namespace MiniClaudeCode.Cli;

/// <summary>
/// CLI argument parser.
///
/// Supported flags:
///   (no args)               Interactive REPL
///   "prompt"                REPL with initial prompt
///   -p "prompt"             Print mode: answer and exit
///   -c                      Continue last conversation
///   -r id                   Resume session by ID
///   --model id              Override model ID
///   --provider name         Override provider (openai/deepseek/zhipu)
///   --add-dir path          Add extra working directory
///   -v, --version           Print version and exit
/// </summary>
public class CliArgs
{
    public const string Version = "0.1.0";

    /// <summary>Run mode.</summary>
    public CliMode Mode { get; set; } = CliMode.Interactive;

    /// <summary>Initial prompt (for REPL-with-prompt or print mode).</summary>
    public string? Prompt { get; set; }

    /// <summary>Model ID override (--model).</summary>
    public string? ModelOverride { get; set; }

    /// <summary>Provider override (--provider).</summary>
    public string? ProviderOverride { get; set; }

    /// <summary>Session ID to resume (-r).</summary>
    public string? ResumeSessionId { get; set; }

    /// <summary>Continue last conversation (-c).</summary>
    public bool ContinueLast { get; set; }

    /// <summary>Extra working directories (--add-dir).</summary>
    public List<string> AddDirs { get; set; } = [];

    /// <summary>Print version and exit (-v, --version).</summary>
    public bool ShowVersion { get; set; }

    /// <summary>
    /// Parse command-line arguments.
    /// </summary>
    public static CliArgs Parse(string[] args)
    {
        var result = new CliArgs();
        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-p" or "--print":
                    result.Mode = CliMode.Print;
                    break;

                case "-c" or "--continue":
                    result.ContinueLast = true;
                    break;

                case "-r" or "--resume":
                    if (i + 1 < args.Length)
                        result.ResumeSessionId = args[++i];
                    break;

                case "--model":
                    if (i + 1 < args.Length)
                        result.ModelOverride = args[++i];
                    break;

                case "--provider":
                    if (i + 1 < args.Length)
                        result.ProviderOverride = args[++i];
                    break;

                case "--add-dir":
                    if (i + 1 < args.Length)
                        result.AddDirs.Add(args[++i]);
                    break;

                case "-v" or "--version":
                    result.ShowVersion = true;
                    break;

                default:
                    // Not a flag — treat as positional (prompt text)
                    positional.Add(args[i]);
                    break;
            }
        }

        // Join positional args as the prompt
        if (positional.Count > 0)
        {
            result.Prompt = string.Join(" ", positional);
            // If no explicit mode set, having a prompt means subagent/one-shot
            if (result.Mode == CliMode.Interactive && !result.ContinueLast)
                result.Mode = CliMode.Print;
        }

        return result;
    }
}

/// <summary>
/// CLI run modes.
/// </summary>
public enum CliMode
{
    /// <summary>Interactive REPL (default).</summary>
    Interactive,

    /// <summary>Print mode: answer prompt and exit.</summary>
    Print,
}

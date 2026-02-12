namespace MiniClaudeCode.Tui.App;

/// <summary>
/// CLI argument parser (carried over from original, with minor updates).
/// </summary>
public class CliArgs
{
    public const string Version = "0.2.0";

    public CliMode Mode { get; set; } = CliMode.Interactive;
    public string? Prompt { get; set; }
    public string? ModelOverride { get; set; }
    public string? ProviderOverride { get; set; }
    public string? ResumeSessionId { get; set; }
    public bool ContinueLast { get; set; }
    public List<string> AddDirs { get; set; } = [];
    public bool ShowVersion { get; set; }

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
                    if (i + 1 < args.Length) result.ResumeSessionId = args[++i];
                    break;
                case "--model":
                    if (i + 1 < args.Length) result.ModelOverride = args[++i];
                    break;
                case "--provider":
                    if (i + 1 < args.Length) result.ProviderOverride = args[++i];
                    break;
                case "--add-dir":
                    if (i + 1 < args.Length) result.AddDirs.Add(args[++i]);
                    break;
                case "-v" or "--version":
                    result.ShowVersion = true;
                    break;
                default:
                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count > 0)
        {
            result.Prompt = string.Join(" ", positional);
            if (result.Mode == CliMode.Interactive && !result.ContinueLast)
                result.Mode = CliMode.Print;
        }

        return result;
    }
}

public enum CliMode
{
    Interactive,
    Print,
}

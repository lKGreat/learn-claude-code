using System.Text;
using System.Text.RegularExpressions;

namespace MiniClaudeCode.Services;

/// <summary>
/// Loads project rules from .cursor/rules/ and AGENTS.md.
///
/// Mirrors Cursor's Rules system:
///   - .cursor/rules/*.md and *.mdc files
///   - AGENTS.md in project root
///   - YAML frontmatter with: description, globs, alwaysApply
///
/// Rule types:
///   - alwaysApply: true  -> always injected into system prompt
///   - alwaysApply: false -> agent decides based on description (loaded on demand)
///   - No frontmatter     -> treated as always-apply (simple markdown rules)
///
/// Rules are included at the start of model context to give
/// the AI consistent guidance for code generation.
/// </summary>
public partial class RulesLoader
{
    private readonly string _workDir;
    private readonly List<RuleInfo> _rules = [];

    public record RuleInfo(
        string Name,
        string? Description,
        string? Globs,
        bool AlwaysApply,
        string Content,
        string FilePath);

    public RulesLoader(string workDir)
    {
        _workDir = workDir;
        LoadRules();
    }

    /// <summary>
    /// Discover and load rules from all supported locations.
    /// </summary>
    private void LoadRules()
    {
        // 1. Load AGENTS.md from project root (simple markdown, always-apply)
        LoadAgentsMd();

        // 2. Load .cursor/rules/ directory
        LoadRulesDirectory(Path.Combine(_workDir, ".cursor", "rules"));

        // 3. Also check .claude/rules/ and .codex/rules/ for compatibility
        LoadRulesDirectory(Path.Combine(_workDir, ".claude", "rules"));
        LoadRulesDirectory(Path.Combine(_workDir, ".codex", "rules"));
    }

    /// <summary>
    /// Load AGENTS.md from project root.
    /// </summary>
    private void LoadAgentsMd()
    {
        var agentsMdPath = Path.Combine(_workDir, "AGENTS.md");
        if (!File.Exists(agentsMdPath))
            return;

        try
        {
            var content = File.ReadAllText(agentsMdPath);
            _rules.Add(new RuleInfo(
                Name: "AGENTS.md",
                Description: null,
                Globs: null,
                AlwaysApply: true,
                Content: content.Trim(),
                FilePath: agentsMdPath));
        }
        catch
        {
            // Skip unreadable files
        }
    }

    /// <summary>
    /// Load all .md and .mdc files from a rules directory.
    /// </summary>
    private void LoadRulesDirectory(string rulesDir)
    {
        if (!Directory.Exists(rulesDir))
            return;

        foreach (var file in Directory.GetFiles(rulesDir, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (ext is not (".md" or ".mdc"))
                continue;

            try
            {
                var rule = ParseRuleFile(file);
                if (rule is not null)
                    _rules.Add(rule);
            }
            catch
            {
                // Skip unparseable files
            }
        }
    }

    /// <summary>
    /// Parse a rule file with optional YAML frontmatter.
    ///
    /// Format:
    ///   ---
    ///   description: "What this rule does"
    ///   globs: "*.ts"
    ///   alwaysApply: true
    ///   ---
    ///   Rule content here...
    /// </summary>
    private RuleInfo? ParseRuleFile(string path)
    {
        var content = File.ReadAllText(path);
        var name = Path.GetFileNameWithoutExtension(path);
        string? description = null;
        string? globs = null;
        bool alwaysApply = true; // Default: always apply if no frontmatter

        // Check for YAML frontmatter
        var match = FrontmatterRegex().Match(content);
        if (match.Success)
        {
            var frontmatter = match.Groups[1].Value;
            content = match.Groups[2].Value.Trim();

            // Parse frontmatter key-value pairs
            foreach (var line in frontmatter.Trim().Split('\n'))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex <= 0) continue;

                var key = line[..colonIndex].Trim().ToLowerInvariant();
                var value = line[(colonIndex + 1)..].Trim().Trim('"', '\'');

                switch (key)
                {
                    case "description":
                        description = value;
                        break;
                    case "globs":
                        globs = value;
                        break;
                    case "alwaysapply":
                        alwaysApply = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(content))
            return null;

        return new RuleInfo(name, description, globs, alwaysApply, content, path);
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Get all always-apply rules content combined, for injection into the system prompt.
    /// </summary>
    public string GetAlwaysApplyRulesContent()
    {
        var alwaysRules = _rules.Where(r => r.AlwaysApply).ToList();
        if (alwaysRules.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("**Project Rules (always apply):**");
        foreach (var rule in alwaysRules)
        {
            sb.AppendLine($"\n--- Rule: {rule.Name} ---");
            sb.AppendLine(rule.Content);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Get descriptions of agent-decided rules (alwaysApply: false).
    /// These are listed in the system prompt so the agent can decide to load them.
    /// </summary>
    public string GetAgentDecidedRulesDescriptions()
    {
        var agentRules = _rules.Where(r => !r.AlwaysApply && !string.IsNullOrEmpty(r.Description)).ToList();
        if (agentRules.Count == 0)
            return "(no on-demand rules)";

        return string.Join("\n", agentRules.Select(r =>
            $"- {r.Name}: {r.Description}" + (r.Globs != null ? $" (files: {r.Globs})" : "")));
    }

    /// <summary>
    /// Get the content of a specific rule by name.
    /// </summary>
    public string? GetRuleContent(string name)
    {
        var rule = _rules.FirstOrDefault(r =>
            r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return rule?.Content;
    }

    /// <summary>
    /// List all available rule names.
    /// </summary>
    public IReadOnlyList<string> ListRules() =>
        _rules.Select(r => r.Name).ToList();

    /// <summary>
    /// List agent-decided rule names only.
    /// </summary>
    public IReadOnlyList<string> ListAgentDecidedRules() =>
        _rules.Where(r => !r.AlwaysApply).Select(r => r.Name).ToList();

    [GeneratedRegex(@"^---\s*\n(.*?)\n---\s*\n(.*)$", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();
}

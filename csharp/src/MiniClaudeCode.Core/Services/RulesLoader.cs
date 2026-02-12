using System.Text;
using System.Text.RegularExpressions;
using MiniClaudeCode.Abstractions.Services;

namespace MiniClaudeCode.Core.Services;

/// <summary>
/// Loads project rules from .cursor/rules/ and AGENTS.md.
/// </summary>
public partial class RulesLoader : IRulesLoader
{
    private readonly string _workDir;
    private readonly List<RuleInfo> _rules = [];

    public record RuleInfo(string Name, string? Description, string? Globs, bool AlwaysApply, string Content, string FilePath);

    public RulesLoader(string workDir)
    {
        _workDir = workDir;
        LoadRules();
    }

    private void LoadRules()
    {
        LoadAgentsMd();
        LoadRulesDirectory(Path.Combine(_workDir, ".cursor", "rules"));
        LoadRulesDirectory(Path.Combine(_workDir, ".claude", "rules"));
        LoadRulesDirectory(Path.Combine(_workDir, ".codex", "rules"));
    }

    private void LoadAgentsMd()
    {
        var agentsMdPath = Path.Combine(_workDir, "AGENTS.md");
        if (!File.Exists(agentsMdPath)) return;

        try
        {
            var content = File.ReadAllText(agentsMdPath);
            _rules.Add(new RuleInfo("AGENTS.md", null, null, true, content.Trim(), agentsMdPath));
        }
        catch { }
    }

    private void LoadRulesDirectory(string rulesDir)
    {
        if (!Directory.Exists(rulesDir)) return;

        foreach (var file in Directory.GetFiles(rulesDir, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (ext is not (".md" or ".mdc")) continue;

            try
            {
                var rule = ParseRuleFile(file);
                if (rule is not null) _rules.Add(rule);
            }
            catch { }
        }
    }

    private RuleInfo? ParseRuleFile(string path)
    {
        var content = File.ReadAllText(path);
        var name = Path.GetFileNameWithoutExtension(path);
        string? description = null;
        string? globs = null;
        bool alwaysApply = true;

        var match = FrontmatterRegex().Match(content);
        if (match.Success)
        {
            var frontmatter = match.Groups[1].Value;
            content = match.Groups[2].Value.Trim();

            foreach (var line in frontmatter.Trim().Split('\n'))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex <= 0) continue;

                var key = line[..colonIndex].Trim().ToLowerInvariant();
                var value = line[(colonIndex + 1)..].Trim().Trim('"', '\'');

                switch (key)
                {
                    case "description": description = value; break;
                    case "globs": globs = value; break;
                    case "alwaysapply": alwaysApply = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(content)) return null;
        return new RuleInfo(name, description, globs, alwaysApply, content, path);
    }

    public string GetAlwaysApplyRulesContent()
    {
        var alwaysRules = _rules.Where(r => r.AlwaysApply).ToList();
        if (alwaysRules.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine("**Project Rules (always apply):**");
        foreach (var rule in alwaysRules)
        {
            sb.AppendLine($"\n--- Rule: {rule.Name} ---");
            sb.AppendLine(rule.Content);
        }
        return sb.ToString();
    }

    public string GetAgentDecidedRulesDescriptions()
    {
        var agentRules = _rules.Where(r => !r.AlwaysApply && !string.IsNullOrEmpty(r.Description)).ToList();
        if (agentRules.Count == 0) return "(no on-demand rules)";
        return string.Join("\n", agentRules.Select(r =>
            $"- {r.Name}: {r.Description}" + (r.Globs != null ? $" (files: {r.Globs})" : "")));
    }

    public string? GetRuleContent(string name)
    {
        var rule = _rules.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return rule?.Content;
    }

    public IReadOnlyList<string> ListRules() => _rules.Select(r => r.Name).ToList();

    public IReadOnlyList<string> ListAgentDecidedRules() =>
        _rules.Where(r => !r.AlwaysApply).Select(r => r.Name).ToList();

    [GeneratedRegex(@"^---\s*\n(.*?)\n---\s*\n(.*)$", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();
}

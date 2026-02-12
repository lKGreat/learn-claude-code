using System.Text;
using System.Text.RegularExpressions;
using MiniClaudeCode.Abstractions.Services;

namespace MiniClaudeCode.Core.Services;

/// <summary>
/// Loads and manages skills from SKILL.md files.
/// </summary>
public partial class SkillLoader : ISkillLoader
{
    private readonly string _skillsDir;
    private readonly Dictionary<string, SkillInfo> _skills = new();

    public record SkillInfo(string Name, string Description, string Body, string Path, string Dir);

    public SkillLoader(string skillsDir)
    {
        _skillsDir = skillsDir;
        LoadSkills();
    }

    private void LoadSkills()
    {
        if (!Directory.Exists(_skillsDir)) return;

        foreach (var skillDir in Directory.GetDirectories(_skillsDir))
        {
            var skillMd = System.IO.Path.Combine(skillDir, "SKILL.md");
            if (!File.Exists(skillMd)) continue;

            var skill = ParseSkillMd(skillMd, skillDir);
            if (skill is not null)
                _skills[skill.Name] = skill;
        }
    }

    private static SkillInfo? ParseSkillMd(string path, string dir)
    {
        try
        {
            var content = File.ReadAllText(path);
            var match = FrontmatterRegex().Match(content);
            if (!match.Success) return null;

            var frontmatter = match.Groups[1].Value;
            var body = match.Groups[2].Value.Trim();

            var metadata = new Dictionary<string, string>();
            foreach (var line in frontmatter.Trim().Split('\n'))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = line[..colonIndex].Trim();
                    var value = line[(colonIndex + 1)..].Trim().Trim('"', '\'');
                    metadata[key] = value;
                }
            }

            if (!metadata.TryGetValue("name", out var name) ||
                !metadata.TryGetValue("description", out var description))
                return null;

            return new SkillInfo(name, description, body, path, dir);
        }
        catch { return null; }
    }

    public string GetDescriptions()
    {
        if (_skills.Count == 0) return "(no skills available)";
        return string.Join("\n", _skills.Select(kv => $"- {kv.Key}: {kv.Value.Description}"));
    }

    public string? GetSkillContent(string name)
    {
        if (!_skills.TryGetValue(name, out var skill)) return null;

        var sb = new StringBuilder();
        sb.AppendLine($"# Skill: {skill.Name}");
        sb.AppendLine();
        sb.AppendLine(skill.Body);

        var resources = new List<string>();
        foreach (var (folder, label) in new[] { ("scripts", "Scripts"), ("references", "References"), ("assets", "Assets") })
        {
            var folderPath = System.IO.Path.Combine(skill.Dir, folder);
            if (!Directory.Exists(folderPath)) continue;
            var files = Directory.GetFiles(folderPath);
            if (files.Length > 0)
            {
                var fileNames = string.Join(", ", files.Select(System.IO.Path.GetFileName));
                resources.Add($"{label}: {fileNames}");
            }
        }

        if (resources.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"**Available resources in {skill.Dir}:**");
            foreach (var r in resources)
                sb.AppendLine($"- {r}");
        }

        return sb.ToString();
    }

    public IReadOnlyList<string> ListSkills() => _skills.Keys.ToList();

    [GeneratedRegex(@"^---\s*\n(.*?)\n---\s*\n(.*)$", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();
}

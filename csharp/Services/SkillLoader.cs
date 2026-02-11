using System.Text;
using System.Text.RegularExpressions;

namespace MiniClaudeCode.Services;

/// <summary>
/// Loads and manages skills from SKILL.md files.
///
/// A skill is a FOLDER containing:
///   - SKILL.md (required): YAML frontmatter + markdown instructions
///   - scripts/ (optional): Helper scripts the model can run
///   - references/ (optional): Additional documentation
///   - assets/ (optional): Templates, files for output
///
/// SKILL.md Format:
///   ---
///   name: pdf
///   description: Process PDF files. Use when reading, creating, or merging PDFs.
///   ---
///
///   # PDF Processing Skill
///   ## Reading PDFs
///   Use pdftotext for quick extraction...
///
/// Progressive Disclosure:
///   Layer 1: Metadata (always loaded) ~100 tokens/skill - name + description
///   Layer 2: SKILL.md body (on trigger) ~2000 tokens - detailed instructions
///   Layer 3: Resources (as needed) - scripts/, references/, assets/
/// </summary>
public partial class SkillLoader
{
    private readonly string _skillsDir;
    private readonly Dictionary<string, SkillInfo> _skills = new();

    public SkillLoader(string skillsDir)
    {
        _skillsDir = skillsDir;
        LoadSkills();
    }

    /// <summary>
    /// Parsed skill information.
    /// </summary>
    public record SkillInfo(
        string Name,
        string Description,
        string Body,
        string Path,
        string Dir);

    /// <summary>
    /// Scan skills directory and load all valid SKILL.md files.
    /// Only loads metadata at startup - body is loaded on-demand.
    /// </summary>
    private void LoadSkills()
    {
        if (!Directory.Exists(_skillsDir))
            return;

        foreach (var skillDir in Directory.GetDirectories(_skillsDir))
        {
            var skillMd = System.IO.Path.Combine(skillDir, "SKILL.md");
            if (!File.Exists(skillMd))
                continue;

            var skill = ParseSkillMd(skillMd, skillDir);
            if (skill is not null)
                _skills[skill.Name] = skill;
        }
    }

    /// <summary>
    /// Parse a SKILL.md file into metadata and body.
    /// Uses simple regex to extract YAML frontmatter (no YAML library needed).
    /// </summary>
    private static SkillInfo? ParseSkillMd(string path, string dir)
    {
        try
        {
            var content = File.ReadAllText(path);

            // Match YAML frontmatter between --- markers
            var match = FrontmatterRegex().Match(content);
            if (!match.Success)
                return null;

            var frontmatter = match.Groups[1].Value;
            var body = match.Groups[2].Value.Trim();

            // Parse YAML-like frontmatter (simple key: value)
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
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get skill descriptions for system prompt (Layer 1).
    /// Only name + description, ~100 tokens per skill.
    /// </summary>
    public string GetDescriptions()
    {
        if (_skills.Count == 0)
            return "(no skills available)";

        return string.Join("\n", _skills.Select(kv =>
            $"- {kv.Key}: {kv.Value.Description}"));
    }

    /// <summary>
    /// Get full skill content for injection (Layer 2 + Layer 3 hints).
    /// Returns null if skill not found.
    /// </summary>
    public string? GetSkillContent(string name)
    {
        if (!_skills.TryGetValue(name, out var skill))
            return null;

        var sb = new StringBuilder();
        sb.AppendLine($"# Skill: {skill.Name}");
        sb.AppendLine();
        sb.AppendLine(skill.Body);

        // List available resources (Layer 3 hints)
        var resources = new List<string>();
        foreach (var (folder, label) in new[] { ("scripts", "Scripts"), ("references", "References"), ("assets", "Assets") })
        {
            var folderPath = System.IO.Path.Combine(skill.Dir, folder);
            if (!Directory.Exists(folderPath))
                continue;

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

    /// <summary>
    /// Return list of available skill names.
    /// </summary>
    public IReadOnlyList<string> ListSkills() => _skills.Keys.ToList();

    [GeneratedRegex(@"^---\s*\n(.*?)\n---\s*\n(.*)$", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();
}

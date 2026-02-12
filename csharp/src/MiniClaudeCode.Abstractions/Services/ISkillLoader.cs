namespace MiniClaudeCode.Abstractions.Services;

/// <summary>
/// Loads and manages skills from SKILL.md files.
/// </summary>
public interface ISkillLoader
{
    /// <summary>Get skill descriptions for system prompt.</summary>
    string GetDescriptions();

    /// <summary>Get full skill content by name. Returns null if not found.</summary>
    string? GetSkillContent(string name);

    /// <summary>Return list of available skill names.</summary>
    IReadOnlyList<string> ListSkills();
}

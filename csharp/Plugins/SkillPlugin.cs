using System.ComponentModel;
using Microsoft.SemanticKernel;
using MiniClaudeCode.Services;

namespace MiniClaudeCode.Plugins;

/// <summary>
/// Skill tool - loads domain knowledge on demand.
///
/// Skills are the difference between Tools (what model CAN do) and
/// Knowledge (how model KNOWS to do it).
///
/// Critical: Skill content goes into tool_result (user message),
/// NOT system prompt. This preserves prompt cache!
///   Wrong: Edit system prompt each time (cache invalidated)
///   Right: Return skill as tool result (prefix unchanged, cache hit)
/// </summary>
public class SkillPlugin
{
    private readonly SkillLoader _loader;

    public SkillPlugin(SkillLoader loader)
    {
        _loader = loader;
    }

    [KernelFunction("Skill")]
    [Description(@"Load a skill to gain specialized knowledge for a task.
Use IMMEDIATELY when user task matches a skill description.
The skill content provides detailed instructions and access to resources.")]
    public string LoadSkill(
        [Description("Name of the skill to load")] string skillName)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n> Loading skill: {skillName}");
        Console.ResetColor();

        var content = _loader.GetSkillContent(skillName);

        if (content is null)
        {
            var available = string.Join(", ", _loader.ListSkills());
            if (string.IsNullOrEmpty(available))
                available = "none";
            return $"Error: Unknown skill '{skillName}'. Available: {available}";
        }

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine($"  Skill loaded ({content.Length} chars)");
        Console.ResetColor();

        // Wrap in tags so model knows it's skill content
        return $"""
            <skill-loaded name="{skillName}">
            {content}
            </skill-loaded>

            Follow the instructions in the skill above to complete the user's task.
            """;
    }
}

using System.ComponentModel;
using Microsoft.SemanticKernel;
using MiniClaudeCode.Abstractions.Services;
using MiniClaudeCode.Abstractions.UI;

namespace MiniClaudeCode.Core.Plugins;

/// <summary>
/// Skill tool - loads domain knowledge on demand.
/// </summary>
public class SkillPlugin
{
    private readonly ISkillLoader _loader;
    private readonly IOutputSink _output;

    public SkillPlugin(ISkillLoader loader, IOutputSink output)
    {
        _loader = loader;
        _output = output;
    }

    [KernelFunction("Skill")]
    [Description(@"Load a skill to gain specialized knowledge for a task.
Use IMMEDIATELY when user task matches a skill description.")]
    public string LoadSkill(
        [Description("Name of the skill to load")] string skillName)
    {
        _output.WriteDebug($"Loading skill: {skillName}");

        var content = _loader.GetSkillContent(skillName);

        if (content is null)
        {
            var available = string.Join(", ", _loader.ListSkills());
            if (string.IsNullOrEmpty(available)) available = "none";
            return $"Error: Unknown skill '{skillName}'. Available: {available}";
        }

        _output.WriteDebug($"Skill loaded ({content.Length} chars)");

        return $"""
            <skill-loaded name="{skillName}">
            {content}
            </skill-loaded>

            Follow the instructions in the skill above to complete the user's task.
            """;
    }
}

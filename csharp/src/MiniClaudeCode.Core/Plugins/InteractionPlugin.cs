using System.ComponentModel;
using Microsoft.SemanticKernel;
using MiniClaudeCode.Abstractions.UI;

namespace MiniClaudeCode.Core.Plugins;

/// <summary>
/// Interaction tool - ask the user clarifying questions.
/// Uses IUserInteraction to decouple from console I/O.
/// </summary>
public class InteractionPlugin
{
    private readonly IUserInteraction _interaction;

    public InteractionPlugin(IUserInteraction interaction)
    {
        _interaction = interaction;
    }

    [KernelFunction("ask_question")]
    [Description(@"Ask the user a question and wait for their response.
Use when you need clarification about requirements, preferences, or ambiguous instructions.")]
    public async Task<string> AskQuestion(
        [Description("The question to ask the user")] string question)
    {
        var answer = await _interaction.AskAsync(question);

        if (string.IsNullOrEmpty(answer))
            return "(user provided no answer)";

        return $"User answered: {answer}";
    }
}

using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace MiniClaudeCode.Plugins;

/// <summary>
/// Interaction tool - ask the user clarifying questions.
///
/// Mirrors Cursor's "Ask questions" capability.
/// Allows the agent to interactively prompt the user when
/// it needs clarification before proceeding with a task.
/// </summary>
public class InteractionPlugin
{
    [KernelFunction("ask_question")]
    [Description(@"Ask the user a question and wait for their response.
Use when you need clarification about requirements, preferences, or ambiguous instructions.
The user will see the question in the console and type their answer.")]
    public string AskQuestion(
        [Description("The question to ask the user")] string question)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n[Agent Question] {question}");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Your answer: ");
        Console.ResetColor();

        string? answer;
        try
        {
            answer = Console.ReadLine()?.Trim();
        }
        catch
        {
            return "(user did not provide an answer)";
        }

        if (string.IsNullOrEmpty(answer))
            return "(user provided no answer)";

        return $"User answered: {answer}";
    }
}

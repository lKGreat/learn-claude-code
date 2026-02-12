using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MiniClaudeCode.Core.Configuration;
using MiniClaudeCode.Core.Services.Providers;

namespace MiniClaudeCode.Core.AI;

/// <summary>
/// Service for processing inline edit requests (Ctrl+K style editing).
/// Takes selected code + user instruction and returns the modified code.
/// </summary>
public class InlineEditService
{
    private readonly Dictionary<ModelProvider, ModelProviderConfig> _providerConfigs;
    private readonly ModelProvider _defaultProvider;

    /// <summary>
    /// Configuration for edit behavior.
    /// </summary>
    public InlineEditConfig Config { get; set; } = new();

    public InlineEditService(
        Dictionary<ModelProvider, ModelProviderConfig> providerConfigs,
        ModelProvider defaultProvider)
    {
        _providerConfigs = providerConfigs;
        _defaultProvider = defaultProvider;
    }

    /// <summary>
    /// Process an inline edit request.
    /// Returns the modified code along with a diff.
    /// </summary>
    public async Task<EditResult> EditCodeAsync(
        EditRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerConfig = _providerConfigs[_defaultProvider];
        var kernel = BuildKernel(providerConfig);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        // Build prompt with full context
        var prompt = BuildEditPrompt(request);

        var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = Config.Temperature,
            MaxTokens = Config.MaxTokens,
            TopP = 0.9,
            FrequencyPenalty = 0.0,
            PresencePenalty = 0.0
        };

        try
        {
            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                kernel,
                cancellationToken);

            var modifiedCode = response.Content?.Trim() ?? "";

            // Extract code from markdown blocks if present
            modifiedCode = ExtractCodeFromMarkdown(modifiedCode, request.Language);

            if (string.IsNullOrWhiteSpace(modifiedCode))
            {
                return new EditResult
                {
                    Success = false,
                    OriginalCode = request.SelectedCode,
                    ModifiedCode = request.SelectedCode,
                    ErrorMessage = "LLM returned empty response"
                };
            }

            // Generate diff
            var diff = GenerateSimpleDiff(request.SelectedCode, modifiedCode);

            return new EditResult
            {
                Success = true,
                OriginalCode = request.SelectedCode,
                ModifiedCode = modifiedCode,
                Diff = diff,
                Instruction = request.Instruction
            };
        }
        catch (Exception ex)
        {
            return new EditResult
            {
                Success = false,
                OriginalCode = request.SelectedCode,
                ModifiedCode = request.SelectedCode,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Build a kernel with just the chat service.
    /// </summary>
    private static Kernel BuildKernel(ModelProviderConfig config)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddProviderChatCompletion(config);
        return builder.Build();
    }

    /// <summary>
    /// Build the edit prompt with context.
    /// </summary>
    private static string BuildEditPrompt(EditRequest request)
    {
        var template = PromptTemplates.InlineEditPrompt;

        return template
            .Replace("{filePath}", request.FilePath)
            .Replace("{language}", request.Language)
            .Replace("{startLine}", request.StartLine.ToString())
            .Replace("{endLine}", request.EndLine.ToString())
            .Replace("{instruction}", request.Instruction)
            .Replace("{selectedCode}", request.SelectedCode)
            .Replace("{contextBefore}", TruncateContext(request.ContextBefore, 50))
            .Replace("{contextAfter}", TruncateContext(request.ContextAfter, 50));
    }

    /// <summary>
    /// Truncate context to reasonable size.
    /// </summary>
    private static string TruncateContext(string text, int maxLines)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var lines = text.Split('\n');
        if (lines.Length <= maxLines)
            return text;

        return string.Join('\n', lines.TakeLast(maxLines));
    }

    /// <summary>
    /// Extract code from markdown code blocks.
    /// </summary>
    private static string ExtractCodeFromMarkdown(string text, string language)
    {
        // Try to find code block with language
        var pattern = $"```{language}\n?(.*?)```";
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            pattern,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (match.Success)
            return match.Groups[1].Value.Trim();

        // Try to find any code block
        pattern = @"```[\w]*\n?(.*?)```";
        match = System.Text.RegularExpressions.Regex.Match(
            text,
            pattern,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (match.Success)
            return match.Groups[1].Value.Trim();

        // No code block found, return as-is
        return text;
    }

    /// <summary>
    /// Generate a simple line-by-line diff.
    /// </summary>
    private static string GenerateSimpleDiff(string original, string modified)
    {
        var originalLines = original.Split('\n');
        var modifiedLines = modified.Split('\n');

        var diff = new StringBuilder();
        diff.AppendLine("--- Original");
        diff.AppendLine("+++ Modified");

        int maxLines = Math.Max(originalLines.Length, modifiedLines.Length);

        for (int i = 0; i < maxLines; i++)
        {
            var origLine = i < originalLines.Length ? originalLines[i] : null;
            var modLine = i < modifiedLines.Length ? modifiedLines[i] : null;

            if (origLine == modLine)
            {
                diff.AppendLine($"  {origLine}");
            }
            else
            {
                if (origLine != null)
                    diff.AppendLine($"- {origLine}");
                if (modLine != null)
                    diff.AppendLine($"+ {modLine}");
            }
        }

        return diff.ToString();
    }

    /// <summary>
    /// Retry an edit with a different instruction (for iterative refinement).
    /// </summary>
    public async Task<EditResult> RefineEditAsync(
        EditResult previousResult,
        string refinementInstruction,
        CancellationToken cancellationToken = default)
    {
        var refinedRequest = new EditRequest
        {
            FilePath = "",
            Language = "",
            StartLine = 0,
            EndLine = 0,
            SelectedCode = previousResult.ModifiedCode,
            Instruction = $"Previous: {previousResult.Instruction}\n\nRefinement: {refinementInstruction}",
            ContextBefore = "",
            ContextAfter = ""
        };

        return await EditCodeAsync(refinedRequest, cancellationToken);
    }
}

/// <summary>
/// Configuration for inline edit behavior.
/// </summary>
public class InlineEditConfig
{
    /// <summary>Temperature for edit generation (0.3 = some creativity).</summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>Maximum tokens to generate for an edit.</summary>
    public int MaxTokens { get; set; } = 2000;
}

/// <summary>
/// Request for inline code editing.
/// </summary>
public record EditRequest
{
    public required string FilePath { get; init; }
    public required string Language { get; init; }
    public required int StartLine { get; init; }
    public required int EndLine { get; init; }
    public required string SelectedCode { get; init; }
    public required string Instruction { get; init; }
    public required string ContextBefore { get; init; }
    public required string ContextAfter { get; init; }
}

/// <summary>
/// Result of an inline edit operation.
/// </summary>
public record EditResult
{
    public required bool Success { get; init; }
    public required string OriginalCode { get; init; }
    public required string ModifiedCode { get; init; }
    public string? Diff { get; init; }
    public string? Instruction { get; init; }
    public string? ErrorMessage { get; init; }
}

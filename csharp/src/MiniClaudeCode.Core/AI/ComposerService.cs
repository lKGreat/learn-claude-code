using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MiniClaudeCode.Abstractions.UI;
using MiniClaudeCode.Core.Agents;
using MiniClaudeCode.Core.Configuration;
using MiniClaudeCode.Core.Plugins;
using MiniClaudeCode.Core.Services.Providers;

namespace MiniClaudeCode.Core.AI;

/// <summary>
/// Multi-file composer orchestration service.
/// Implements a two-phase approach: planning and execution.
/// Uses Semantic Kernel agents with full tool access for planning,
/// focused sub-agents for execution.
/// </summary>
public class ComposerService
{
    private readonly Dictionary<ModelProvider, ModelProviderConfig> _providerConfigs;
    private readonly ModelProvider _defaultProvider;
    private readonly string _workDir;
    private readonly IOutputSink _output;
    private readonly AgentRegistry _registry;

    public ComposerService(
        Dictionary<ModelProvider, ModelProviderConfig> providerConfigs,
        ModelProvider defaultProvider,
        string workDir,
        IOutputSink output,
        AgentRegistry registry)
    {
        _providerConfigs = providerConfigs;
        _defaultProvider = defaultProvider;
        _workDir = workDir;
        _output = output;
        _registry = registry;
    }

    /// <summary>
    /// Generate a structured execution plan for a multi-file change request.
    /// Phase 1 of the composer workflow.
    /// </summary>
    public async Task<ComposerPlan?> GeneratePlanAsync(
        ComposerRequest request,
        CancellationToken cancellationToken = default)
    {
        _output.WriteSystem("Composer: Generating plan...");

        var providerConfig = _providerConfigs[_defaultProvider];
        var kernel = BuildPlanningKernel(providerConfig);

        // Build planning prompt with codebase context
        var prompt = await BuildPlanningPromptAsync(request, cancellationToken);

        var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.2, // Lower temperature for structured planning
            MaxTokens = 4000,
            ResponseFormat = "json_object" // Request JSON response if supported
        };

        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                kernel,
                cancellationToken);

            var planJson = response.Content?.Trim() ?? "";

            // Extract JSON if wrapped in markdown
            planJson = ExtractJsonFromMarkdown(planJson);

            if (string.IsNullOrWhiteSpace(planJson))
            {
                _output.WriteError("Composer: LLM returned empty plan");
                return null;
            }

            // Parse JSON plan
            var plan = JsonSerializer.Deserialize<ComposerPlan>(planJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

            if (plan == null)
            {
                _output.WriteError("Composer: Failed to parse plan JSON");
                return null;
            }

            _output.WriteSystem($"Composer: Plan generated with {plan.Steps.Count} steps");
            return plan;
        }
        catch (Exception ex)
        {
            _output.WriteError($"Composer: Plan generation failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Execute a composer plan.
    /// Phase 2 of the composer workflow.
    /// </summary>
    public async Task<ComposerResult> ExecutePlanAsync(
        ComposerPlan plan,
        CancellationToken cancellationToken = default)
    {
        _output.WriteSystem($"Composer: Executing plan with {plan.Steps.Count} steps...");

        var result = new ComposerResult
        {
            Plan = plan,
            StartTime = DateTimeOffset.UtcNow,
            StepResults = new List<StepResult>()
        };

        // Sort steps by dependencies (topological sort)
        var orderedSteps = TopologicalSort(plan.Steps);

        foreach (var step in orderedSteps)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.Status = ComposerStatus.Cancelled;
                break;
            }

            _output.WriteSystem($"Composer: Step {step.StepNumber}: {step.Description}");

            var stepResult = await ExecuteStepAsync(step, cancellationToken);
            result.StepResults.Add(stepResult);

            if (!stepResult.Success)
            {
                _output.WriteError($"Composer: Step {step.StepNumber} failed: {stepResult.ErrorMessage}");
                result.Status = ComposerStatus.Failed;
                break;
            }

            _output.WriteSystem($"Composer: Step {step.StepNumber} completed");
        }

        if (result.Status != ComposerStatus.Failed && result.Status != ComposerStatus.Cancelled)
        {
            result.Status = ComposerStatus.Completed;
        }

        result.EndTime = DateTimeOffset.UtcNow;
        _output.WriteSystem($"Composer: Execution {result.Status.ToString().ToLowerInvariant()} in {result.Duration.TotalSeconds:F1}s");

        return result;
    }

    /// <summary>
    /// Execute a single step from the plan.
    /// </summary>
    private async Task<StepResult> ExecuteStepAsync(
        ComposerStep step,
        CancellationToken cancellationToken)
    {
        var stepResult = new StepResult
        {
            StepNumber = step.StepNumber,
            FilePath = step.File,
            Action = step.Action
        };

        try
        {
            switch (step.Action.ToLowerInvariant())
            {
                case "create":
                    stepResult.Success = await ExecuteCreateAsync(step);
                    break;

                case "modify":
                    stepResult.Success = await ExecuteModifyAsync(step);
                    break;

                case "delete":
                    stepResult.Success = await ExecuteDeleteAsync(step);
                    break;

                default:
                    stepResult.Success = false;
                    stepResult.ErrorMessage = $"Unknown action: {step.Action}";
                    break;
            }
        }
        catch (Exception ex)
        {
            stepResult.Success = false;
            stepResult.ErrorMessage = ex.Message;
        }

        return stepResult;
    }

    /// <summary>
    /// Execute a "create" step: create a new file.
    /// </summary>
    private async Task<bool> ExecuteCreateAsync(ComposerStep step)
    {
        var filePath = Path.Combine(_workDir, step.File);
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Aggregate all "insert" changes into file content
        var content = new StringBuilder();
        foreach (var change in step.Changes.OrderBy(c => c.LineNumber))
        {
            content.AppendLine(change.Replace);
        }

        await File.WriteAllTextAsync(filePath, content.ToString());
        return true;
    }

    /// <summary>
    /// Execute a "modify" step: apply changes to an existing file.
    /// </summary>
    private async Task<bool> ExecuteModifyAsync(ComposerStep step)
    {
        var filePath = Path.Combine(_workDir, step.File);

        if (!File.Exists(filePath))
        {
            _output.WriteError($"File not found: {step.File}");
            return false;
        }

        var content = await File.ReadAllTextAsync(filePath);

        // Apply each change in order
        foreach (var change in step.Changes)
        {
            switch (change.Type.ToLowerInvariant())
            {
                case "replace":
                    if (!content.Contains(change.Search))
                    {
                        _output.WriteWarning($"Search text not found: {change.Search[..Math.Min(50, change.Search.Length)]}...");
                        return false;
                    }
                    content = content.Replace(change.Search, change.Replace);
                    break;

                case "insert":
                    var lines = content.Split('\n').ToList();
                    if (change.LineNumber >= 0 && change.LineNumber <= lines.Count)
                    {
                        lines.Insert(change.LineNumber, change.Replace);
                        content = string.Join('\n', lines);
                    }
                    break;

                case "delete":
                    if (!string.IsNullOrEmpty(change.Search))
                    {
                        content = content.Replace(change.Search, "");
                    }
                    break;
            }
        }

        await File.WriteAllTextAsync(filePath, content);
        return true;
    }

    /// <summary>
    /// Execute a "delete" step: delete a file.
    /// </summary>
    private Task<bool> ExecuteDeleteAsync(ComposerStep step)
    {
        var filePath = Path.Combine(_workDir, step.File);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }

        _output.WriteWarning($"File not found (already deleted?): {step.File}");
        return Task.FromResult(true); // Not an error if already deleted
    }

    /// <summary>
    /// Refine a plan based on user feedback.
    /// </summary>
    public async Task<ComposerPlan?> RefinePlanAsync(
        ComposerPlan originalPlan,
        string feedback,
        CancellationToken cancellationToken = default)
    {
        _output.WriteSystem("Composer: Refining plan based on feedback...");

        var request = new ComposerRequest
        {
            Description = $"Original request: {originalPlan.Summary}\n\nUser feedback: {feedback}",
            CodebaseContext = ""
        };

        return await GeneratePlanAsync(request, cancellationToken);
    }

    /// <summary>
    /// Build the planning prompt with codebase context.
    /// </summary>
    private async Task<string> BuildPlanningPromptAsync(
        ComposerRequest request,
        CancellationToken cancellationToken)
    {
        var template = PromptTemplates.ComposerPlanPrompt;

        // Build project structure (file tree)
        var projectStructure = await BuildProjectStructureAsync(cancellationToken);

        return template
            .Replace("{request}", request.Description)
            .Replace("{codebaseContext}", request.CodebaseContext)
            .Replace("{projectStructure}", projectStructure);
    }

    /// <summary>
    /// Build a representation of the project structure.
    /// </summary>
    private async Task<string> BuildProjectStructureAsync(CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Project files:");

        try
        {
            var files = Directory.GetFiles(_workDir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(_workDir, f))
                .Where(f => !ShouldSkipPath(f))
                .OrderBy(f => f)
                .Take(200); // Limit to prevent token overflow

            foreach (var file in files)
            {
                sb.AppendLine($"  {file}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  (error: {ex.Message})");
        }

        await Task.CompletedTask;
        return sb.ToString();
    }

    /// <summary>
    /// Check if a path should be skipped in project structure.
    /// </summary>
    private static bool ShouldSkipPath(string relativePath)
    {
        string[] skipDirs = [".git", "node_modules", "bin", "obj", ".vs", "__pycache__", ".venv", "venv"];
        foreach (var dir in skipDirs)
        {
            if (relativePath.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Build a kernel with full plugins for planning.
    /// </summary>
    private Kernel BuildPlanningKernel(ModelProviderConfig config)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddProviderChatCompletion(config);

        // Add read-only plugins for exploration
        builder.Plugins.AddFromObject(new FileSearchPlugin(_workDir), "FileSearchPlugin");

        return builder.Build();
    }

    /// <summary>
    /// Extract JSON from markdown code blocks.
    /// </summary>
    private static string ExtractJsonFromMarkdown(string text)
    {
        var pattern = @"```json\s*(.*?)```";
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            pattern,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (match.Success)
            return match.Groups[1].Value.Trim();

        // Try to find any JSON-like content
        var jsonStart = text.IndexOf('{');
        var jsonEnd = text.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
            return text[jsonStart..(jsonEnd + 1)];

        return text;
    }

    /// <summary>
    /// Topological sort of steps by dependencies.
    /// </summary>
    private static List<ComposerStep> TopologicalSort(List<ComposerStep> steps)
    {
        var sorted = new List<ComposerStep>();
        var visited = new HashSet<int>();

        void Visit(ComposerStep step)
        {
            if (visited.Contains(step.StepNumber))
                return;

            // Visit dependencies first
            foreach (var depNum in step.Dependencies)
            {
                var dep = steps.FirstOrDefault(s => s.StepNumber == depNum);
                if (dep != null)
                    Visit(dep);
            }

            visited.Add(step.StepNumber);
            sorted.Add(step);
        }

        foreach (var step in steps.OrderBy(s => s.StepNumber))
        {
            Visit(step);
        }

        return sorted;
    }
}

/// <summary>
/// Request to the composer service.
/// </summary>
public record ComposerRequest
{
    public required string Description { get; init; }
    public required string CodebaseContext { get; init; }
}

/// <summary>
/// Structured plan for multi-file changes.
/// </summary>
public class ComposerPlan
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = "";

    [JsonPropertyName("steps")]
    public List<ComposerStep> Steps { get; set; } = new();

    [JsonPropertyName("validation")]
    public ValidationInfo? Validation { get; set; }
}

/// <summary>
/// A single step in a composer plan.
/// </summary>
public class ComposerStep
{
    [JsonPropertyName("stepNumber")]
    public int StepNumber { get; set; }

    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = ""; // create, modify, delete

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("dependencies")]
    public List<int> Dependencies { get; set; } = new();

    [JsonPropertyName("changes")]
    public List<CodeChange> Changes { get; set; } = new();
}

/// <summary>
/// A single code change within a step.
/// </summary>
public class CodeChange
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // replace, insert, delete

    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; set; }

    [JsonPropertyName("search")]
    public string Search { get; set; } = "";

    [JsonPropertyName("replace")]
    public string Replace { get; set; } = "";

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = "";
}

/// <summary>
/// Validation information for the plan.
/// </summary>
public class ValidationInfo
{
    [JsonPropertyName("tests")]
    public List<string> Tests { get; set; } = new();

    [JsonPropertyName("manualChecks")]
    public List<string> ManualChecks { get; set; } = new();
}

/// <summary>
/// Result of executing a composer plan.
/// </summary>
public class ComposerResult
{
    public required ComposerPlan Plan { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public DateTimeOffset? EndTime { get; set; }
    public TimeSpan Duration => (EndTime ?? DateTimeOffset.UtcNow) - StartTime;
    public ComposerStatus Status { get; set; } = ComposerStatus.Running;
    public required List<StepResult> StepResults { get; init; }
}

/// <summary>
/// Result of executing a single step.
/// </summary>
public class StepResult
{
    public required int StepNumber { get; init; }
    public required string FilePath { get; init; }
    public required string Action { get; init; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of composer execution.
/// </summary>
public enum ComposerStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}

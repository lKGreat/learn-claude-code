# Integration Guide: AI Services in MiniClaudeCode

This guide shows how to integrate the AI services into the MiniClaudeCode engine.

## Step 1: Extend EngineContext

Add AI service properties to `EngineContext`:

```csharp
// File: Configuration/EngineContext.cs
public class EngineContext
{
    // ... existing properties ...

    // AI Services
    public required InlineCompletionService CompletionService { get; init; }
    public required InlineEditService EditService { get; init; }
    public required ComposerService ComposerService { get; init; }
    public required ContextBuilder ContextBuilder { get; init; }
}
```

## Step 2: Update EngineBuilder

Modify `EngineBuilder.Build()` to instantiate AI services:

```csharp
// File: Configuration/EngineBuilder.cs
public EngineContext Build()
{
    if (_interaction == null)
        throw new InvalidOperationException("IUserInteraction must be provided.");

    var activeConfig = _providerConfigs[_activeProvider];

    // Services
    var todoManager = new TodoManager();
    var skillLoader = new SkillLoader(Path.Combine(_workDir, "skills"));
    var rulesLoader = new RulesLoader(_workDir);
    var agentRegistry = new AgentRegistry();

    // Agent system
    var subAgentRunner = new SubAgentRunner(
        _providerConfigs, _activeProvider, _agentProviderOverrides,
        _workDir, agentRegistry, _output, _progressReporter);
    var parallelExecutor = new ParallelAgentExecutor(subAgentRunner);
    var agentTeam = new AgentTeam(subAgentRunner, parallelExecutor, _output);

    // AI Services (NEW)
    var completionService = new InlineCompletionService(
        _providerConfigs,
        _activeProvider,
        debounceDelay: TimeSpan.FromMilliseconds(800));

    var editService = new InlineEditService(
        _providerConfigs,
        _activeProvider);

    var composerService = new ComposerService(
        _providerConfigs,
        _activeProvider,
        _workDir,
        _output,
        agentRegistry);

    var contextBuilder = new ContextBuilder(_workDir);

    // Build kernel with all plugins
    var builder = Kernel.CreateBuilder();
    builder.AddProviderChatCompletion(activeConfig);

    builder.Plugins.AddFromObject(new CodingPlugin(_workDir, _output), "CodingPlugin");
    builder.Plugins.AddFromObject(new FileSearchPlugin(_workDir), "FileSearchPlugin");
    builder.Plugins.AddFromObject(new WebPlugin(_output), "WebPlugin");
    builder.Plugins.AddFromObject(new TodoPlugin(todoManager, _output), "TodoPlugin");
    builder.Plugins.AddFromObject(new TaskPlugin(subAgentRunner, _output), "TaskPlugin");
    builder.Plugins.AddFromObject(new SkillPlugin(skillLoader, _output), "SkillPlugin");
    builder.Plugins.AddFromObject(new RulesPlugin(rulesLoader, _output), "RulesPlugin");
    builder.Plugins.AddFromObject(new InteractionPlugin(_interaction), "InteractionPlugin");

    var kernel = builder.Build();

    // Build system prompt
    var systemPrompt = BuildSystemPrompt(_workDir, skillLoader, rulesLoader);

    // Create main agent
    var agent = new ChatCompletionAgent
    {
        Name = "MiniClaudeCode",
        Instructions = systemPrompt,
        Kernel = kernel,
        Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        })
    };

    return new EngineContext
    {
        Kernel = kernel,
        Agent = agent,
        Thread = new ChatHistoryAgentThread(),
        WorkDir = _workDir,
        ActiveConfig = activeConfig,
        ProviderConfigs = _providerConfigs,
        AgentProviderOverrides = _agentProviderOverrides,
        SkillLoader = skillLoader,
        RulesLoader = rulesLoader,
        TodoManager = todoManager,
        AgentRegistry = agentRegistry,
        SubAgentRunner = subAgentRunner,
        ParallelExecutor = parallelExecutor,
        AgentTeam = agentTeam,
        Output = _output,
        ProgressReporter = _progressReporter,

        // AI Services (NEW)
        CompletionService = completionService,
        EditService = editService,
        ComposerService = composerService,
        ContextBuilder = contextBuilder
    };
}
```

## Step 3: UI Integration Examples

### A. Inline Completion (Autocomplete)

Wire up in your text editor's `TextChanged` event:

```csharp
// File: Avalonia/ViewModels/EditorViewModel.cs
private CancellationTokenSource? _completionCts;

private async void OnTextChanged(object? sender, EventArgs e)
{
    // Cancel previous completion request
    _completionCts?.Cancel();
    _completionCts = new CancellationTokenSource();

    var cursor = Editor.CaretOffset;
    var line = Editor.Document.GetLineByOffset(cursor);
    var lineNumber = line.LineNumber;
    var column = cursor - line.Offset;

    var codeBefore = Editor.Text[..cursor];
    var codeAfter = Editor.Text[cursor..];

    var request = new CompletionRequest
    {
        FilePath = CurrentFilePath,
        Language = DetectLanguage(CurrentFilePath),
        Line = lineNumber,
        Column = column,
        CodeBefore = codeBefore,
        CodeAfter = codeAfter
    };

    try
    {
        var result = await _engineContext.CompletionService.GetCompletionAsync(
            request,
            _completionCts.Token);

        if (result != null)
        {
            ShowInlineCompletion(result.Text);
        }
    }
    catch (OperationCanceledException)
    {
        // Superseded by newer request
    }
}

private void ShowInlineCompletion(string completionText)
{
    // Display as ghost text (gray, italic)
    // User presses Tab to accept, Esc to dismiss
    InlineCompletionOverlay.Text = completionText;
    InlineCompletionOverlay.IsVisible = true;
}
```

### B. Inline Edit (Ctrl+K)

Create a command for inline editing:

```csharp
// File: Avalonia/ViewModels/EditorViewModel.cs
[RelayCommand]
private async Task InlineEdit()
{
    var selection = Editor.SelectedText;
    if (string.IsNullOrEmpty(selection))
        return;

    // Show input dialog for instruction
    var instruction = await ShowInputDialogAsync("Enter editing instruction:");
    if (string.IsNullOrEmpty(instruction))
        return;

    var startLine = Editor.Document.GetLineByOffset(Editor.SelectionStart).LineNumber;
    var endLine = Editor.Document.GetLineByOffset(Editor.SelectionStart + Editor.SelectionLength).LineNumber;

    // Build context
    var contextBuilder = _engineContext.ContextBuilder;
    var editContext = await contextBuilder.BuildEditContextAsync(
        CurrentFilePath,
        startLine - 1, // 0-based
        endLine - 1,
        CancellationToken.None);

    var request = new EditRequest
    {
        FilePath = CurrentFilePath,
        Language = editContext.Language,
        StartLine = startLine,
        EndLine = endLine,
        SelectedCode = selection,
        Instruction = instruction,
        ContextBefore = editContext.ContextBefore,
        ContextAfter = editContext.ContextAfter
    };

    // Show loading indicator
    IsEditingInline = true;

    try
    {
        var result = await _engineContext.EditService.EditCodeAsync(
            request,
            CancellationToken.None);

        if (result.Success)
        {
            // Show diff preview
            var accept = await ShowDiffPreviewAsync(result.OriginalCode, result.ModifiedCode, result.Diff);
            if (accept)
            {
                Editor.SelectedText = result.ModifiedCode;
            }
        }
        else
        {
            await ShowErrorAsync($"Edit failed: {result.ErrorMessage}");
        }
    }
    finally
    {
        IsEditingInline = false;
    }
}
```

### C. Multi-File Composer

Create a composer panel UI:

```csharp
// File: Avalonia/ViewModels/ComposerViewModel.cs
[RelayCommand]
private async Task GeneratePlan()
{
    if (string.IsNullOrWhiteSpace(UserRequest))
        return;

    IsGeneratingPlan = true;
    StatusMessage = "Analyzing request and generating plan...";

    try
    {
        var request = new ComposerRequest
        {
            Description = UserRequest,
            CodebaseContext = BuildCodebaseContext()
        };

        var plan = await _engineContext.ComposerService.GeneratePlanAsync(
            request,
            CancellationToken.None);

        if (plan != null)
        {
            CurrentPlan = plan;
            PlanSteps.Clear();
            foreach (var step in plan.Steps)
            {
                PlanSteps.Add(new StepViewModel(step));
            }

            StatusMessage = $"Plan generated with {plan.Steps.Count} steps. Review and execute?";
        }
        else
        {
            StatusMessage = "Failed to generate plan.";
        }
    }
    finally
    {
        IsGeneratingPlan = false;
    }
}

[RelayCommand]
private async Task ExecutePlan()
{
    if (CurrentPlan == null)
        return;

    IsExecutingPlan = true;
    StatusMessage = "Executing plan...";

    try
    {
        var result = await _engineContext.ComposerService.ExecutePlanAsync(
            CurrentPlan,
            CancellationToken.None);

        if (result.Status == ComposerStatus.Completed)
        {
            StatusMessage = $"Plan completed successfully in {result.Duration.TotalSeconds:F1}s";

            // Refresh file tree and editor
            await RefreshWorkspaceAsync();
        }
        else
        {
            StatusMessage = $"Plan execution {result.Status.ToString().ToLowerInvariant()}";

            // Show failed steps
            var failedSteps = result.StepResults.Where(s => !s.Success).ToList();
            await ShowFailedStepsDialogAsync(failedSteps);
        }
    }
    finally
    {
        IsExecutingPlan = false;
    }
}

[RelayCommand]
private async Task RefinePlan()
{
    if (CurrentPlan == null)
        return;

    var feedback = await ShowInputDialogAsync("Enter refinement feedback:");
    if (string.IsNullOrEmpty(feedback))
        return;

    IsGeneratingPlan = true;
    StatusMessage = "Refining plan...";

    try
    {
        var refinedPlan = await _engineContext.ComposerService.RefinePlanAsync(
            CurrentPlan,
            feedback,
            CancellationToken.None);

        if (refinedPlan != null)
        {
            CurrentPlan = refinedPlan;
            PlanSteps.Clear();
            foreach (var step in refinedPlan.Steps)
            {
                PlanSteps.Add(new StepViewModel(step));
            }

            StatusMessage = "Plan refined.";
        }
    }
    finally
    {
        IsGeneratingPlan = false;
    }
}

private string BuildCodebaseContext()
{
    // Build context from open files and workspace
    var sb = new StringBuilder();
    sb.AppendLine("Open files:");
    foreach (var file in OpenFiles)
    {
        sb.AppendLine($"  - {file.RelativePath}");
    }
    return sb.ToString();
}
```

## Step 4: Configuration

Add environment variables for AI service configuration:

```bash
# .env file
OPENAI_API_KEY=sk-...
OPENAI_MODEL_ID=gpt-4o
DEEPSEEK_API_KEY=...
DEEPSEEK_MODEL_ID=deepseek-chat

# Agent-specific overrides
AGENT_COMPLETION_PROVIDER=deepseek  # Use fast model for completions
```

## Step 5: Error Handling

Implement global error handling for AI operations:

```csharp
// File: Avalonia/Services/AIErrorHandler.cs
public class AIErrorHandler
{
    private readonly IOutputSink _output;

    public AIErrorHandler(IOutputSink output)
    {
        _output = output;
    }

    public async Task<T?> ExecuteWithErrorHandlingAsync<T>(
        Func<Task<T>> operation,
        string operationName)
    {
        try
        {
            return await operation();
        }
        catch (HttpRequestException ex)
        {
            _output.WriteError($"{operationName} failed: Network error - {ex.Message}");
            return default;
        }
        catch (TaskCanceledException)
        {
            _output.WriteWarning($"{operationName} was cancelled");
            return default;
        }
        catch (Exception ex)
        {
            _output.WriteError($"{operationName} failed: {ex.Message}");
            return default;
        }
    }
}
```

Usage:

```csharp
var errorHandler = new AIErrorHandler(_output);

var result = await errorHandler.ExecuteWithErrorHandlingAsync(
    async () => await _engineContext.CompletionService.GetCompletionAsync(request, ct),
    "Inline completion");
```

## Step 6: Testing

Create integration tests:

```csharp
// File: Tests/AIServicesIntegrationTests.cs
public class AIServicesIntegrationTests
{
    private EngineContext _context = null!;

    [SetUp]
    public void Setup()
    {
        var providerConfigs = new Dictionary<ModelProvider, ModelProviderConfig>
        {
            [ModelProvider.OpenAI] = new()
            {
                Provider = ModelProvider.OpenAI,
                ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
                ModelId = "gpt-4o"
            }
        };

        _context = new EngineBuilder()
            .WithProviders(providerConfigs, ModelProvider.OpenAI)
            .WithWorkDir(Path.Combine(Path.GetTempPath(), "test-workspace"))
            .WithOutputSink(new TestOutputSink())
            .WithUserInteraction(new TestUserInteraction())
            .WithProgressReporter(new TestProgressReporter())
            .Build();
    }

    [Test]
    public async Task InlineCompletion_CompletesFunction()
    {
        var request = new CompletionRequest
        {
            FilePath = "test.cs",
            Language = "csharp",
            Line = 1,
            Column = 15,
            CodeBefore = "public void Process",
            CodeAfter = "{\n}"
        };

        var result = await _context.CompletionService.GetCompletionAsync(request);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Text, Is.Not.Empty);
    }

    [Test]
    public async Task InlineEdit_AddsNullChecks()
    {
        var request = new EditRequest
        {
            FilePath = "test.cs",
            Language = "csharp",
            StartLine = 1,
            EndLine = 1,
            SelectedCode = "public int Add(int a, int b) => a + b;",
            Instruction = "Add parameter validation",
            ContextBefore = "class Calculator {",
            ContextAfter = "}"
        };

        var result = await _context.EditService.EditCodeAsync(request);

        Assert.That(result.Success, Is.True);
        Assert.That(result.ModifiedCode, Does.Contain("ArgumentNullException")
            .Or.Contain("throw")
            .Or.Contain("if"));
    }

    [Test]
    public async Task Composer_GeneratesValidPlan()
    {
        var request = new ComposerRequest
        {
            Description = "Add logging to all public methods",
            CodebaseContext = "Project has 3 service classes"
        };

        var plan = await _context.ComposerService.GeneratePlanAsync(request);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan!.Steps, Is.Not.Empty);
        Assert.That(plan.Summary, Is.Not.Empty);
    }
}
```

## Step 7: Performance Monitoring

Add telemetry:

```csharp
// File: Services/AITelemetry.cs
public class AITelemetry
{
    private readonly IOutputSink _output;
    private readonly Dictionary<string, List<double>> _latencies = new();

    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> func)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await func();
        }
        finally
        {
            sw.Stop();
            RecordLatency(operation, sw.Elapsed.TotalMilliseconds);
        }
    }

    private void RecordLatency(string operation, double milliseconds)
    {
        if (!_latencies.ContainsKey(operation))
            _latencies[operation] = new List<double>();

        _latencies[operation].Add(milliseconds);

        if (_latencies[operation].Count % 10 == 0)
        {
            var avg = _latencies[operation].Average();
            var p95 = _latencies[operation].OrderBy(x => x).Skip((int)(_latencies[operation].Count * 0.95)).First();
            _output.WriteDebug($"AI Telemetry [{operation}]: avg={avg:F0}ms, p95={p95:F0}ms");
        }
    }
}
```

## Summary

The integration consists of:

1. **EngineBuilder extension**: Add AI service initialization
2. **EngineContext extension**: Add AI service properties
3. **UI integration**: Wire up completion, edit, and composer in ViewModels
4. **Error handling**: Graceful degradation and user feedback
5. **Testing**: Integration tests for core scenarios
6. **Telemetry**: Performance monitoring

All services are designed to be non-blocking and cancellable, ensuring the editor remains responsive even during AI operations.

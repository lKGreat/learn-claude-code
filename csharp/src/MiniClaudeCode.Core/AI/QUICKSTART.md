# Quick Start Guide: AI Services

Get up and running with MiniClaudeCode AI services in 5 minutes.

## Prerequisites

1. **.NET 10 SDK** installed
2. **OpenAI API key** (or DeepSeek/Zhipu)
3. **Project cloned** and building

## Step 1: Configure API Keys

Create a `.env` file in the project root:

```bash
OPENAI_API_KEY=sk-your-key-here
OPENAI_MODEL_ID=gpt-4o

# Optional: Add DeepSeek for faster completions
DEEPSEEK_API_KEY=your-deepseek-key
DEEPSEEK_MODEL_ID=deepseek-chat

# Use DeepSeek for completions (faster and cheaper)
AGENT_COMPLETION_PROVIDER=deepseek
```

Load environment variables in your app startup:

```csharp
// Program.cs or App.axaml.cs
DotNetEnv.Env.Load(); // Requires DotNetEnv NuGet package
```

## Step 2: Initialize Services

In your existing `EngineBuilder` setup:

```csharp
// This code already exists in your app
var providerConfigs = ModelProviderConfig.LoadAll();
var activeProvider = ModelProviderConfig.ResolveActiveProvider(providerConfigs);

var engineContext = new EngineBuilder()
    .WithProviders(providerConfigs, activeProvider)
    .WithWorkDir(workspaceDir)
    .WithOutputSink(outputSink)
    .WithUserInteraction(userInteraction)
    .WithProgressReporter(progressReporter)
    .Build();

// Services are now available:
// - engineContext.CompletionService
// - engineContext.EditService
// - engineContext.ComposerService
// - engineContext.ContextBuilder
```

## Step 3: Use Inline Completion

### A. Basic Usage

```csharp
using MiniClaudeCode.Core.AI;

// When user types in editor
private async Task OnTextChangedAsync(int cursorOffset, string fullText)
{
    var request = new CompletionRequest
    {
        FilePath = "src/Program.cs",
        Language = "csharp",
        Line = GetCurrentLine(cursorOffset),
        Column = GetCurrentColumn(cursorOffset),
        CodeBefore = fullText[..cursorOffset],
        CodeAfter = fullText[cursorOffset..]
    };

    var result = await _engineContext.CompletionService.GetCompletionAsync(
        request,
        cancellationToken);

    if (result != null)
    {
        ShowGhostText(result.Text); // Display as gray italic text
    }
}

// When user presses Tab
private void OnAcceptCompletion()
{
    InsertTextAtCursor(_currentCompletion);
    HideGhostText();
}

// When user presses Escape
private void OnDismissCompletion()
{
    HideGhostText();
}
```

### B. With Debouncing

```csharp
private CancellationTokenSource? _completionCts;

private async Task RequestCompletionAsync()
{
    // Cancel previous request
    _completionCts?.Cancel();
    _completionCts = new CancellationTokenSource();

    // Service handles debouncing automatically (800ms default)
    var result = await _engineContext.CompletionService.GetCompletionAsync(
        request,
        _completionCts.Token);

    // Will be null if cancelled by newer request
    if (result != null)
    {
        ShowGhostText(result.Text);
    }
}
```

## Step 4: Use Inline Edit

### A. Basic Usage

```csharp
// When user presses Ctrl+K
private async Task OnInlineEditAsync()
{
    var selectedText = Editor.SelectedText;
    if (string.IsNullOrEmpty(selectedText))
        return;

    // Show input dialog
    var instruction = await ShowInputDialogAsync("What would you like to change?");
    if (string.IsNullOrEmpty(instruction))
        return;

    var request = new EditRequest
    {
        FilePath = CurrentFilePath,
        Language = DetectLanguage(CurrentFilePath),
        StartLine = Editor.SelectionStartLine,
        EndLine = Editor.SelectionEndLine,
        SelectedCode = selectedText,
        Instruction = instruction,
        ContextBefore = GetLinesBefore(Editor.SelectionStartLine, 50),
        ContextAfter = GetLinesAfter(Editor.SelectionEndLine, 50)
    };

    // Show loading
    ShowLoadingOverlay("Editing code...");

    var result = await _engineContext.EditService.EditCodeAsync(
        request,
        CancellationToken.None);

    HideLoadingOverlay();

    if (result.Success)
    {
        // Show diff preview
        var accepted = await ShowDiffDialogAsync(
            result.OriginalCode,
            result.ModifiedCode,
            result.Diff);

        if (accepted)
        {
            Editor.SelectedText = result.ModifiedCode;
        }
    }
    else
    {
        ShowErrorDialog($"Edit failed: {result.ErrorMessage}");
    }
}
```

### B. With Refinement

```csharp
// If user wants to refine the edit
private async Task OnRefineEditAsync(EditResult previousResult)
{
    var refinement = await ShowInputDialogAsync(
        "How should we refine the edit?",
        placeholder: "E.g., 'Add error handling', 'Use async/await'");

    if (string.IsNullOrEmpty(refinement))
        return;

    var refined = await _engineContext.EditService.RefineEditAsync(
        previousResult,
        refinement,
        CancellationToken.None);

    // Show new diff
    await ShowDiffDialogAsync(
        previousResult.ModifiedCode,
        refined.ModifiedCode,
        refined.Diff);
}
```

## Step 5: Use Composer

### A. Generate Plan

```csharp
// When user opens composer panel
private async Task OnGeneratePlanAsync()
{
    var userRequest = ComposerRequestTextBox.Text;
    if (string.IsNullOrWhiteSpace(userRequest))
        return;

    StatusText = "Analyzing request and generating plan...";
    IsGeneratingPlan = true;

    var request = new ComposerRequest
    {
        Description = userRequest,
        CodebaseContext = BuildCodebaseContext() // List of open files, etc.
    };

    var plan = await _engineContext.ComposerService.GeneratePlanAsync(
        request,
        CancellationToken.None);

    IsGeneratingPlan = false;

    if (plan != null)
    {
        CurrentPlan = plan;
        DisplayPlan(plan); // Show in UI
        StatusText = $"Plan generated with {plan.Steps.Count} steps. Review and execute?";
    }
    else
    {
        StatusText = "Failed to generate plan.";
    }
}

private string BuildCodebaseContext()
{
    var sb = new StringBuilder();
    sb.AppendLine("Open files:");
    foreach (var file in OpenFiles)
    {
        sb.AppendLine($"  - {file.RelativePath}");
    }
    return sb.ToString();
}
```

### B. Display Plan

```csharp
private void DisplayPlan(ComposerPlan plan)
{
    PlanSummaryText.Text = plan.Summary;
    PlanReasoningText.Text = plan.Reasoning;

    PlanStepsListBox.Items.Clear();
    foreach (var step in plan.Steps)
    {
        var stepItem = new PlanStepViewModel
        {
            StepNumber = step.StepNumber,
            File = step.File,
            Action = step.Action,
            Description = step.Description,
            ChangeCount = step.Changes.Count
        };
        PlanStepsListBox.Items.Add(stepItem);
    }
}
```

### C. Execute Plan

```csharp
private async Task OnExecutePlanAsync()
{
    if (CurrentPlan == null)
        return;

    // Confirm with user
    var confirmed = await ShowConfirmDialogAsync(
        $"Execute plan with {CurrentPlan.Steps.Count} steps?",
        "This will modify your files.");

    if (!confirmed)
        return;

    StatusText = "Executing plan...";
    IsExecutingPlan = true;

    var result = await _engineContext.ComposerService.ExecutePlanAsync(
        CurrentPlan,
        CancellationToken.None);

    IsExecutingPlan = false;

    if (result.Status == ComposerStatus.Completed)
    {
        StatusText = $"Plan completed in {result.Duration.TotalSeconds:F1}s";
        await RefreshWorkspaceAsync(); // Reload file tree and editor
        await ShowSuccessDialogAsync("Composer execution completed successfully!");
    }
    else
    {
        StatusText = $"Plan execution {result.Status.ToString().ToLowerInvariant()}";

        // Show which steps failed
        var failedSteps = result.StepResults
            .Where(s => !s.Success)
            .Select(s => $"Step {s.StepNumber}: {s.ErrorMessage}")
            .ToList();

        await ShowErrorDialogAsync(
            "Some steps failed:\n" + string.Join("\n", failedSteps));
    }
}
```

### D. Refine Plan

```csharp
private async Task OnRefinePlanAsync()
{
    if (CurrentPlan == null)
        return;

    var feedback = await ShowInputDialogAsync(
        "How should we refine the plan?",
        placeholder: "E.g., 'Skip step 3', 'Add logging to all methods'");

    if (string.IsNullOrEmpty(feedback))
        return;

    StatusText = "Refining plan...";
    IsGeneratingPlan = true;

    var refinedPlan = await _engineContext.ComposerService.RefinePlanAsync(
        CurrentPlan,
        feedback,
        CancellationToken.None);

    IsGeneratingPlan = false;

    if (refinedPlan != null)
    {
        CurrentPlan = refinedPlan;
        DisplayPlan(refinedPlan);
        StatusText = "Plan refined.";
    }
}
```

## Step 6: Context Building (Optional)

If you need custom context for your own AI operations:

```csharp
var contextBuilder = _engineContext.ContextBuilder;

// For completions (lightweight)
var completionContext = await contextBuilder.BuildCompletionContextAsync(
    filePath: "src/Program.cs",
    cursorLine: 42,
    cursorColumn: 15,
    openFiles: new[] { "src/Helper.cs", "src/Config.cs" },
    cancellationToken);

Console.WriteLine($"Estimated tokens: {completionContext.EstimatedTokens}");

// For chat (heavy)
var chatContext = await contextBuilder.BuildChatContextAsync(
    currentFile: "src/Program.cs",
    mentionedFiles: new[] { "src/Helper.cs", "README.md" },
    userMessage: "How does authentication work?",
    cancellationToken);

// For edits (medium)
var editContext = await contextBuilder.BuildEditContextAsync(
    filePath: "src/Auth.cs",
    startLine: 10,
    endLine: 20,
    cancellationToken);
```

## Common Patterns

### Error Handling

```csharp
try
{
    var result = await _engineContext.CompletionService.GetCompletionAsync(
        request,
        cancellationToken);

    if (result == null)
    {
        // Request was cancelled or failed gracefully
        return;
    }

    // Use result
    ShowGhostText(result.Text);
}
catch (HttpRequestException ex)
{
    ShowErrorNotification("Network error: " + ex.Message);
}
catch (TaskCanceledException)
{
    // User cancelled, ignore
}
catch (Exception ex)
{
    ShowErrorNotification("Unexpected error: " + ex.Message);
}
```

### Progress Feedback

```csharp
// For long-running operations
var cts = new CancellationTokenSource();

// Show progress dialog with cancel button
var progressDialog = ShowProgressDialog("Generating plan...", cts);

try
{
    var plan = await _engineContext.ComposerService.GeneratePlanAsync(
        request,
        cts.Token);

    progressDialog.Close();

    if (plan != null)
    {
        // Success
    }
}
catch (OperationCanceledException)
{
    progressDialog.Close();
    ShowNotification("Operation cancelled by user");
}
```

### Configuration

```csharp
// Adjust service behavior at runtime
_engineContext.CompletionService.Config.Temperature = 0.1; // More deterministic
_engineContext.CompletionService.Config.MaxTokens = 150;   // Shorter completions

_engineContext.EditService.Config.Temperature = 0.5;       // More creative
_engineContext.EditService.Config.MaxTokens = 3000;        // Longer edits

_engineContext.ContextBuilder.BudgetConfig.MaxContextTokens = 10000; // Bigger context
```

## Debugging Tips

### Enable Verbose Logging

```csharp
// In development, log all AI operations
_engineContext.Output.WriteDebug("Requesting completion...");
var sw = Stopwatch.StartNew();
var result = await _engineContext.CompletionService.GetCompletionAsync(request, ct);
sw.Stop();
_engineContext.Output.WriteDebug($"Completion returned in {sw.ElapsedMilliseconds}ms");
```

### Test with Mock Provider

```csharp
// Create a test kernel with mock responses
var mockService = new MockChatCompletionService();
mockService.AddResponse("public int Add(int a, int b) => a + b;");

var builder = Kernel.CreateBuilder();
builder.Services.AddSingleton<IChatCompletionService>(mockService);
var kernel = builder.Build();

// Use kernel for testing
```

### Inspect Prompts

```csharp
// Print the actual prompt being sent
var prompt = PromptTemplates.CompletionPrompt
    .Replace("{filePath}", request.FilePath)
    .Replace("{language}", request.Language)
    // ... etc
Console.WriteLine(prompt);
```

## Performance Tips

1. **Adjust debounce delay**: Shorter for faster feedback, longer for fewer API calls
2. **Use cache**: Keep cache enabled for repeated requests
3. **Optimize context**: Don't send entire files if only selection is needed
4. **Prefer fast models**: Use DeepSeek for completions
5. **Batch operations**: Use composer for multi-file changes instead of individual edits

## Next Steps

1. **Read INTEGRATION.md** for detailed UI integration examples
2. **Read README.md** for architecture overview
3. **Write tests** using examples in CHECKLIST.md
4. **Customize prompts** in PromptTemplates.cs for your use case
5. **Add telemetry** to track performance and usage

## Troubleshooting

### Completions are slow
- Check network latency to API endpoint
- Use a faster model (DeepSeek instead of GPT-4)
- Reduce `MaxTokens` in config
- Increase debounce delay to batch requests

### Completions are incorrect
- Adjust `Temperature` (lower = more deterministic)
- Improve context by sending more surrounding code
- Customize `CompletionPrompt` template
- Try a more capable model (GPT-4 instead of GPT-3.5)

### Edits fail
- Check that instruction is clear and specific
- Ensure enough context is provided
- Try refining the edit with more details
- Check error message for LLM's explanation

### Composer doesn't find files
- Verify `WorkDir` is set correctly
- Check that files exist in workspace
- Look at generated plan to see what files were discovered
- Add more context to the request

## Support

See main project README for contribution guidelines and support channels.

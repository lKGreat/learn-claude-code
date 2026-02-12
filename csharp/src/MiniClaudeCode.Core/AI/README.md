# AI Agent Strategy Implementation

This directory contains the AI agent strategy for MiniClaudeCode's inline completion, inline edit, and multi-file composer features. Built on **Microsoft Semantic Kernel 1.x**.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      MiniClaudeCode UI                      │
│                   (Avalonia Editor)                         │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│                    Core AI Services                          │
├─────────────────────────────────────────────────────────────┤
│  InlineCompletionService  │  InlineEditService              │
│  (fast, cached)           │  (medium speed)                 │
├───────────────────────────┴─────────────────────────────────┤
│  ComposerService                                             │
│  (planning + execution)                                      │
├──────────────────────────────────────────────────────────────┤
│  ContextBuilder                                              │
│  (token budget management)                                   │
└─────────────────┬────────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│              Semantic Kernel + Agents                        │
│  • ChatCompletionAgent                                       │
│  • SubAgentRunner (5 agent types)                           │
│  • Multi-provider support (OpenAI, DeepSeek, Zhipu)         │
└──────────────────────────────────────────────────────────────┘
```

## Components

### 1. PromptTemplates.cs

**Purpose**: Centralized repository of all prompt templates.

**Templates**:
- `CompletionPrompt`: Inline code completion (autocomplete)
- `InlineEditPrompt`: Ctrl+K style inline editing
- `ComposerPlanPrompt`: Multi-file change planning
- `ComposerExecutePrompt`: Step execution
- `CodeActionPrompt`: Quick fix suggestions
- `InlineChatPrompt`: Editor-embedded Q&A
- `CommitMessagePrompt`: Git commit message generation
- `TestGenerationPrompt`: Unit test generation
- `DocumentationPrompt`: XML doc generation

**Design principles**:
- Clear instructions with structured output formats
- Context-aware (file path, language, cursor position)
- Token-efficient (avoid redundancy)
- Defensive (handle edge cases gracefully)

### 2. InlineCompletionService.cs

**Purpose**: High-performance autocomplete with debouncing and caching.

**Key features**:
- **Debouncing**: 800ms default delay (configurable)
- **Caching**: SHA256-based cache keys with 5-minute TTL
- **Lightweight**: Uses `IChatCompletionService` directly (no agent overhead)
- **Fast model preference**: Prefers DeepSeek if available
- **Token limits**: Max 200 tokens per completion
- **Deterministic**: Temperature = 0.0

**Performance optimizations**:
- Cancellation token propagation for rapid request superseding
- LRU cache eviction (max 100 entries)
- Context truncation (500 lines before, 200 lines after cursor)
- Markdown cleanup (removes code blocks from LLM output)

**Usage**:
```csharp
var service = new InlineCompletionService(
    providerConfigs,
    ModelProvider.DeepSeek,
    debounceDelay: TimeSpan.FromMilliseconds(800));

var request = new CompletionRequest
{
    FilePath = "src/Program.cs",
    Language = "csharp",
    Line = 42,
    Column = 15,
    CodeBefore = "public void Process",
    CodeAfter = "{\n    // ...\n}"
};

var result = await service.GetCompletionAsync(request, cancellationToken);
Console.WriteLine(result?.Text); // "Data(string input)"
```

### 3. InlineEditService.cs

**Purpose**: Process Ctrl+K inline edit requests.

**Key features**:
- **Context-aware**: Analyzes code before/after selection
- **Instruction-driven**: User provides natural language instruction
- **Diff generation**: Produces line-by-line diff
- **Iterative refinement**: `RefineEditAsync` for multi-turn editing
- **Temperature**: 0.3 (some creativity for better edits)
- **Max tokens**: 2000

**Workflow**:
1. User selects code + provides instruction
2. Service builds prompt with selection + context
3. LLM returns modified code
4. Service extracts code (strips markdown if present)
5. Generates diff between original and modified
6. Returns `EditResult` with both versions + diff

**Usage**:
```csharp
var service = new InlineEditService(providerConfigs, ModelProvider.OpenAI);

var request = new EditRequest
{
    FilePath = "src/Calculator.cs",
    Language = "csharp",
    StartLine = 10,
    EndLine = 15,
    SelectedCode = "int Add(int a, int b) { return a + b; }",
    Instruction = "Add null checks and XML documentation",
    ContextBefore = "public class Calculator {",
    ContextAfter = "}"
};

var result = await service.EditCodeAsync(request, cancellationToken);
Console.WriteLine(result.ModifiedCode);
Console.WriteLine(result.Diff);
```

### 4. ComposerService.cs

**Purpose**: Multi-file composer with two-phase orchestration.

**Architecture**: Planner-Executor pattern
- **Phase 1: Planning**: Analyze request → generate JSON plan
- **Phase 2: Execution**: Execute plan steps sequentially

**JSON Plan Schema**:
```json
{
  "summary": "Brief description",
  "reasoning": "Why this approach",
  "steps": [
    {
      "stepNumber": 1,
      "file": "src/MyFile.cs",
      "action": "create|modify|delete",
      "description": "What this step does",
      "dependencies": [0],
      "changes": [
        {
          "type": "replace|insert|delete",
          "lineNumber": 42,
          "search": "exact code to find",
          "replace": "new code",
          "rationale": "Why"
        }
      ]
    }
  ],
  "validation": {
    "tests": ["Test to run"],
    "manualChecks": ["Things to verify"]
  }
}
```

**Step types**:
- `create`: Create new file with content
- `modify`: Apply changes to existing file
- `delete`: Remove file

**Change types**:
- `replace`: Find exact text and replace
- `insert`: Insert at line number
- `delete`: Remove text

**Features**:
- Topological sort by dependencies
- Atomic step execution
- Rollback support (future enhancement)
- Plan refinement loop

**Usage**:
```csharp
var service = new ComposerService(
    providerConfigs,
    ModelProvider.OpenAI,
    workDir,
    output,
    registry);

var request = new ComposerRequest
{
    Description = "Add logging to all service classes",
    CodebaseContext = "Services are in src/Services/*.cs"
};

// Phase 1: Generate plan
var plan = await service.GeneratePlanAsync(request, cancellationToken);
output.WriteSystem($"Plan has {plan.Steps.Count} steps");

// Phase 2: Execute plan
var result = await service.ExecutePlanAsync(plan, cancellationToken);
output.WriteSystem($"Completed in {result.Duration.TotalSeconds}s");

// Optional: Refine plan
if (needsRefinement)
{
    var refinedPlan = await service.RefinePlanAsync(
        plan,
        "Add error handling to step 3",
        cancellationToken);
}
```

### 5. ContextBuilder.cs

**Purpose**: Build context for AI operations with token budget management.

**Context strategies**:

| Operation   | Strategy                                      | Token Budget |
|-------------|-----------------------------------------------|--------------|
| Completion  | Current file ±100 lines + open file names    | 2,000        |
| Chat        | @mentioned files + current file + symbols    | 8,000        |
| Edit        | Selected code + full file + import context   | 4,000        |

**Token estimation**: Uses `chars / 3.5` heuristic (production should use tiktoken).

**Budget allocation** (for chat):
- 60% main content
- 30% context files
- 10% system prompt

**Truncation strategy**: For large files, keeps beginning + end, omits middle.

**Language detection**: 30+ file extensions supported (C#, JS, TS, Python, Go, Rust, etc.)

**Usage**:
```csharp
var builder = new ContextBuilder(workDir);
builder.BudgetConfig.MaxContextTokens = 8000;

// Completion context
var completionCtx = await builder.BuildCompletionContextAsync(
    "src/Program.cs",
    cursorLine: 42,
    cursorColumn: 15,
    openFiles: new[] { "src/Helper.cs", "src/Config.cs" },
    cancellationToken);

Console.WriteLine($"Tokens: {completionCtx.EstimatedTokens}");
Console.WriteLine($"Language: {completionCtx.Language}");

// Chat context
var chatCtx = await builder.BuildChatContextAsync(
    currentFile: "src/Program.cs",
    mentionedFiles: new[] { "@src/Helper.cs", "@README.md" },
    userMessage: "How does authentication work?",
    cancellationToken);

// Edit context
var editCtx = await builder.BuildEditContextAsync(
    "src/Auth.cs",
    startLine: 10,
    endLine: 20,
    cancellationToken);
```

### 6. SubAgentRunner Updates

**New agent type: `completion`**

Optimized for fast inline completions:
- **No tools**: Bypasses plugin system for speed
- **Temperature**: 0.0 (deterministic)
- **Max tokens**: 200
- **Model tier**: Prefers "fast" tier (DeepSeek)

**Updated agent types**:
```csharp
["completion"] = new(
    Description: "Lightweight fast agent for inline code completions",
    Tools: [],
    Prompt: "Generate completions quickly. Return ONLY completion text.",
    IsReadOnly: true
);
```

**Integration**:
```csharp
var task = new AgentTask
{
    Description = "Complete function signature",
    Prompt = "public void Process",
    AgentType = "completion",
    ModelTier = "fast"
};

var result = await subAgentRunner.RunAsync(task, cancellationToken);
```

## Integration with EngineBuilder

The AI services are integrated into the main `EngineContext` via `EngineBuilder`:

```csharp
public class EngineBuilder
{
    // ... existing code ...

    public EngineContext Build()
    {
        // Build AI services
        var completionService = new InlineCompletionService(
            _providerConfigs,
            _activeProvider);

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

        return new EngineContext
        {
            // ... existing services ...
            CompletionService = completionService,
            EditService = editService,
            ComposerService = composerService,
            ContextBuilder = contextBuilder
        };
    }
}
```

## Performance Characteristics

| Operation        | Latency Target | Token Limit | Caching | Concurrent |
|------------------|----------------|-------------|---------|------------|
| Inline Completion| < 1s           | 200         | ✅      | ✅         |
| Inline Edit      | < 3s           | 2,000       | ❌      | ❌         |
| Composer Planning| < 10s          | 4,000       | ❌      | ❌         |
| Composer Execute | Variable       | Variable    | ❌      | Sequential |

## Configuration

All services support configuration via properties:

**InlineCompletionService**:
```csharp
service.Config.Temperature = 0.0;
service.Config.MaxTokens = 200;
service.Config.CacheTTL = TimeSpan.FromMinutes(5);
service.Config.MaxCacheSize = 100;
```

**InlineEditService**:
```csharp
service.Config.Temperature = 0.3;
service.Config.MaxTokens = 2000;
```

**ContextBuilder**:
```csharp
builder.BudgetConfig.MaxContextTokens = 8000;
builder.BudgetConfig.MaxEditContextTokens = 4000;
builder.BudgetConfig.MaxCompletionContextTokens = 2000;
```

## Multi-Provider Support

All services inherit provider configuration from `EngineBuilder`:
- **OpenAI**: GPT-4, GPT-3.5
- **DeepSeek**: deepseek-chat, deepseek-coder
- **Zhipu**: glm-4-plus

**Model tier selection**:
- `"fast"`: Prefer DeepSeek (cheapest, fast)
- `"default"`: Use active provider
- Agent-specific overrides via environment variables

## Error Handling

All services implement defensive error handling:
- Cancellation token support throughout
- Graceful degradation (return empty/original on error)
- Error logging to console
- Never crash the editor

## Future Enhancements

1. **Streaming completions**: Use `IAsyncEnumerable<StreamingChatMessageContent>` for real-time feedback
2. **Proper tokenization**: Replace char-based estimation with tiktoken
3. **Context caching**: Cache file contents, not just completion results
4. **Rollback support**: Implement transaction-based composer execution
5. **Telemetry**: Add performance metrics and usage analytics
6. **Fine-tuned models**: Support custom fine-tuned models for completions
7. **Multi-file edit**: Extend inline edit to modify multiple files atomically

## Testing

Unit test examples:

```csharp
[Fact]
public async Task InlineCompletion_ReturnsValidCompletion()
{
    var service = new InlineCompletionService(
        TestProviderConfigs,
        ModelProvider.OpenAI);

    var request = new CompletionRequest
    {
        FilePath = "test.cs",
        Language = "csharp",
        Line = 1,
        Column = 15,
        CodeBefore = "public void ",
        CodeAfter = "()"
    };

    var result = await service.GetCompletionAsync(request);
    Assert.NotNull(result);
    Assert.NotEmpty(result.Text);
}
```

## License

Same as parent project (MIT).

## Contact

See main project README for contribution guidelines.

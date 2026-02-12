# Implementation Checklist

## ✅ Phase 1: Core Implementation (COMPLETED)

- [x] Create `PromptTemplates.cs` with 9 prompt templates
- [x] Create `InlineCompletionService.cs` with debouncing and caching
- [x] Create `InlineEditService.cs` with diff generation
- [x] Create `ComposerService.cs` with planning and execution
- [x] Create `ContextBuilder.cs` with token budget management
- [x] Update `SubAgentRunner.cs` to add "completion" agent type
- [x] Add proper using statements for `AddProviderChatCompletion` extension
- [x] Create comprehensive documentation (README.md, INTEGRATION.md, SUMMARY.md)

## 🔄 Phase 2: EngineBuilder Integration (TODO)

### 2.1 Update EngineContext.cs

Location: `D:\Code\learn-claude-code\csharp\src\MiniClaudeCode.Core\Configuration\EngineContext.cs`

Add these properties:
```csharp
public required InlineCompletionService CompletionService { get; init; }
public required InlineEditService EditService { get; init; }
public required ComposerService ComposerService { get; init; }
public required ContextBuilder ContextBuilder { get; init; }
```

### 2.2 Update EngineBuilder.cs

Location: `D:\Code\learn-claude-code\csharp\src\MiniClaudeCode.Core\Configuration\EngineBuilder.cs`

Add to `Build()` method (after agent system initialization):
```csharp
// AI Services
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
```

Add to return statement:
```csharp
CompletionService = completionService,
EditService = editService,
ComposerService = composerService,
ContextBuilder = contextBuilder
```

Add using statement:
```csharp
using MiniClaudeCode.Core.AI;
```

## 🔄 Phase 3: Avalonia UI Integration (TODO)

### 3.1 EditorViewModel - Inline Completion

File: `csharp/src/MiniClaudeCode.Avalonia/ViewModels/EditorViewModel.cs`

Add:
- `_completionCts` field for cancellation
- `OnTextChanged` handler for completion requests
- `ShowInlineCompletion` method for ghost text
- `AcceptCompletion` command (Tab key)
- `DismissCompletion` command (Esc key)

### 3.2 EditorViewModel - Inline Edit

Add:
- `InlineEditCommand` (Ctrl+K)
- Input dialog for instruction
- Diff preview dialog
- Accept/reject buttons

### 3.3 ComposerViewModel - Multi-File Composer

Create new file: `csharp/src/MiniClaudeCode.Avalonia/ViewModels/ComposerViewModel.cs`

Add:
- `UserRequest` property (observable)
- `CurrentPlan` property (observable)
- `PlanSteps` collection (observable)
- `GeneratePlanCommand`
- `ExecutePlanCommand`
- `RefinePlanCommand`
- Status and progress properties

### 3.4 ComposerView - UI Panel

Create new file: `csharp/src/MiniClaudeCode.Avalonia/Views/ComposerView.axaml`

Add:
- TextBox for user request
- Button for "Generate Plan"
- ListBox for plan steps (with expand/collapse)
- Buttons for "Execute", "Refine", "Cancel"
- Progress bar and status text

### 3.5 KeyBindings

File: Update main window or editor control

Add:
- `Tab` → AcceptCompletion (when completion visible)
- `Escape` → DismissCompletion (when completion visible)
- `Ctrl+K` → InlineEdit (when text selected)
- `Ctrl+Shift+P` then "Composer" → OpenComposer

## 🔄 Phase 4: Testing (TODO)

### 4.1 Unit Tests

Create: `csharp/tests/MiniClaudeCode.Core.Tests/AI/`

Files to create:
- `InlineCompletionServiceTests.cs`
- `InlineEditServiceTests.cs`
- `ComposerServiceTests.cs`
- `ContextBuilderTests.cs`
- `PromptTemplatesTests.cs` (validate template structure)

### 4.2 Integration Tests

Create: `csharp/tests/MiniClaudeCode.Integration.Tests/AI/`

Files to create:
- `AIServicesIntegrationTests.cs` (end-to-end with real LLM)
- `MultiProviderTests.cs` (OpenAI, DeepSeek, Zhipu)
- `ErrorRecoveryTests.cs`

### 4.3 UI Tests

Create: `csharp/tests/MiniClaudeCode.Avalonia.Tests/`

Files to create:
- `CompletionUITests.cs`
- `InlineEditUITests.cs`
- `ComposerUITests.cs`

## 🔄 Phase 5: Configuration (TODO)

### 5.1 Environment Variables

Document in main README:
```bash
# AI service configuration
AGENT_COMPLETION_PROVIDER=deepseek
AI_COMPLETION_DEBOUNCE_MS=800
AI_COMPLETION_CACHE_SIZE=100
AI_EDIT_TEMPERATURE=0.3
AI_CONTEXT_MAX_TOKENS=8000
```

### 5.2 Settings UI

Create settings panel:
- Enable/disable inline completion
- Adjust debounce delay
- Select model provider for each service
- Configure token budgets

## 🔄 Phase 6: Polish (TODO)

### 6.1 Visual Feedback

Implement:
- Ghost text styling (gray, italic) for completions
- Loading spinner overlay for inline edit
- Progress bar for composer
- Toast notifications for errors
- Syntax highlighting in diff preview

### 6.2 Error Handling

Implement:
- Retry logic with exponential backoff
- Fallback to manual editing on errors
- User-friendly error messages
- Network status indicator

### 6.3 Performance Monitoring

Implement:
- Latency tracking (P50, P95, P99)
- Cache hit rate logging
- Token usage tracking
- API cost estimation

### 6.4 Documentation

Write:
- User guide for AI features
- Video tutorial for composer
- Troubleshooting guide
- FAQ section

## 🔄 Phase 7: Advanced Features (FUTURE)

### 7.1 Streaming Completions

Replace `GetChatMessageContentAsync` with:
```csharp
await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(...))
{
    // Update ghost text incrementally
}
```

### 7.2 Context Caching

Implement:
- File content cache with invalidation
- Shared cache across services
- Persistent cache (optional)

### 7.3 Proper Tokenization

Replace char-based estimation with tiktoken:
```csharp
using Microsoft.ML.Tokenizers;
var tokenizer = Tokenizer.CreateTiktokenForModel("gpt-4");
var tokens = tokenizer.Encode(text);
```

### 7.4 Composer Rollback

Implement:
- Git integration for automatic commits before composer
- Rollback command to undo composer changes
- Checkpoint/restore mechanism

### 7.5 Fine-Tuned Models

Support:
- Custom model endpoints
- Language-specific models
- User-uploaded fine-tuned models

## 📊 Success Metrics

Track these metrics to measure success:

| Metric | Target | How to Measure |
|--------|--------|----------------|
| Completion latency | < 500ms P95 | Telemetry in InlineCompletionService |
| Completion acceptance rate | > 30% | Track Tab key presses after completion shown |
| Edit success rate | > 80% | Track accepted vs. rejected edits |
| Composer success rate | > 70% | Track completed vs. failed executions |
| Cache hit rate | > 40% | Log cache hits/misses in InlineCompletionService |
| User satisfaction | > 4/5 | In-app survey after 10 AI operations |

## 🚀 Deployment Checklist

Before releasing:

- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Performance benchmarks meet targets
- [ ] Documentation is complete
- [ ] User guide is written
- [ ] Error messages are user-friendly
- [ ] Keyboard shortcuts are documented
- [ ] Settings UI is functional
- [ ] Multi-provider support is tested
- [ ] Edge cases are handled gracefully

## 📝 Version Control

Recommended commit strategy:

1. `feat(ai): add prompt templates and core services`
2. `feat(ai): add inline completion service with caching`
3. `feat(ai): add inline edit service with diff generation`
4. `feat(ai): add composer service with planning and execution`
5. `feat(ai): add context builder with token management`
6. `feat(agents): add completion agent type to SubAgentRunner`
7. `feat(ai): integrate AI services into EngineBuilder`
8. `feat(ui): add inline completion UI with ghost text`
9. `feat(ui): add inline edit command with diff preview`
10. `feat(ui): add composer panel with plan visualization`
11. `test(ai): add unit tests for AI services`
12. `test(ai): add integration tests with real LLM`
13. `docs(ai): add user guide for AI features`

## 🐛 Known Issues to Address

1. **Token estimation accuracy**: Replace char/3.5 with proper tokenizer
2. **Cache persistence**: Add optional persistent cache
3. **Rate limiting**: Add protection against API rate limits
4. **Cost tracking**: Add token usage and cost tracking
5. **Prompt injection**: Add input sanitization
6. **Parallel composer**: Add parallel step execution (where safe)
7. **Context sharing**: Share common context across services
8. **Model selection UI**: Add model selection per operation

## 🎯 Priority Order

1. **P0 (Must Have)**: Phase 1 ✅, Phase 2 🔄
2. **P1 (Should Have)**: Phase 3, Phase 4.1, Phase 5.1
3. **P2 (Nice to Have)**: Phase 4.2, Phase 5.2, Phase 6.1, Phase 6.2
4. **P3 (Future)**: Phase 4.3, Phase 6.3, Phase 6.4, Phase 7

---

**Current Status**: Phase 1 complete ✅, Phase 2 ready for implementation 🔄

**Next Action**: Update `EngineBuilder.cs` and `EngineContext.cs` as documented above.

**Estimated Time to MVP**: 2-3 days (Phase 1-3 complete)

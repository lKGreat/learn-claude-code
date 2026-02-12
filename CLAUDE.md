# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

All C# projects live under `csharp/`. The solution uses .NET 10.0 and the `.slnx` format.

```bash
# Build entire solution
dotnet build csharp/MiniClaudeCode.slnx

# Run the Avalonia desktop app
dotnet run --project csharp/src/MiniClaudeCode.Avalonia

# Build a specific project
dotnet build csharp/src/MiniClaudeCode.Core
```

There are no test projects or CI/CD pipelines in this repo currently.

## Environment Configuration

Copy `csharp/.env.example` to `csharp/.env` and configure:
- `ACTIVE_PROVIDER` — select `openai`, `deepseek`, or `zhipu`
- Provider-specific API keys and model IDs (e.g. `OPENAI_API_KEY`, `OPENAI_MODEL_ID`, `OPENAI_BASE_URL`)
- Per-agent-type model overrides: `AGENT_EXPLORE_PROVIDER`, `AGENT_CODE_PROVIDER`, `AGENT_PLAN_PROVIDER`

## Architecture

MiniClaudeCode is a multi-provider AI coding agent built on **Microsoft Semantic Kernel 1.x**, with a VS Code-style **Avalonia** desktop UI.

### Layer Diagram

```
Avalonia (UI) → Core (Engine) → Abstractions (Contracts)
                  ↑
               Rpc (gRPC scaffolding, future use)
```

**Abstractions** — Pure interfaces and DTOs. Defines UI contracts (`IOutputSink`, `IUserInteraction`, `IAgentProgressReporter`, `IToolCallObserver`), agent models, and service contracts. No dependencies.

**Core** — Agent orchestration engine, completely UI-independent. Key areas:
- `Configuration/EngineBuilder` — Fluent builder that wires up the entire engine (providers, plugins, services)
- `Configuration/ModelProviderConfig` — Multi-provider support (OpenAI/DeepSeek/Zhipu) loaded from env vars
- `Agents/` — `AgentRegistry` (lifecycle + TTL cleanup), `AgentTeam`, `ParallelAgentExecutor`, `SubAgentRunner`
- `Plugins/` — 8 Semantic Kernel plugins: `CodingPlugin` (bash, file I/O, grep), `FileSearchPlugin`, `WebPlugin`, `TodoPlugin`, `TaskPlugin`, `SkillPlugin`, `RulesPlugin`, `InteractionPlugin`
- `Services/` — `RulesLoader`, `SkillLoader`, `TodoManager`

**Avalonia** — Cross-platform desktop frontend using MVVM (CommunityToolkit.Mvvm).
- `Adapters/` — 4 adapter classes bridge engine callbacks to the UI by implementing Abstractions interfaces. All use `Dispatcher.UIThread.Post()` for thread safety. This is the key decoupling layer between engine and UI.
- `ViewModels/` — MVVM ViewModels with source-generated `[ObservableProperty]` and `[RelayCommand]`
- `Models/` — UI-only data models (ChatMessage, AgentItem, TodoItem, etc.)
- `Services/` — UI services (Git, workspace, file watching)
- `Views/Editor/` — Piece-tree text buffer and TextMate syntax highlighting

**Rpc** — gRPC service definitions in `Protos/agent.proto`. Scaffolded but not fully implemented.

### Key Patterns

- The engine communicates with any UI through 4 adapter interfaces defined in Abstractions. To support a new frontend, implement `IOutputSink`, `IUserInteraction`, `IAgentProgressReporter`, and `IToolCallObserver`.
- Semantic Kernel agents use `ChatCompletionAgent` with `FunctionChoiceBehavior.Auto()` for tool selection.
- Sub-agents (explore, code, plan) can be spawned in parallel via `TaskPlugin` → `SubAgentRunner`.
- Suppressed warnings `SKEXP0001`, `SKEXP0110`, `OPENAI001` are for Semantic Kernel experimental APIs — this is intentional in `Directory.Build.props`.

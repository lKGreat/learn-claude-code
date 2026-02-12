---
name: Avalonia Cross-Platform UI
overview: Add a new MiniClaudeCode.Avalonia project using Avalonia UI 11 with MVVM pattern, replicating all Cursor IDE interactions (chat, sub-agents, parallel exploration, rules, todos, ask questions) with a modern dark-themed cross-platform desktop UI.
todos:
  - id: scaffold-project
    content: Create MiniClaudeCode.Avalonia.csproj with Avalonia 11 + Semi.Avalonia + CommunityToolkit.Mvvm dependencies, add to solution, create Program.cs and App.axaml entry points
    status: completed
  - id: models-converters
    content: Create UI models (ChatMessage, ToolCallItem, TodoItem) and value converters (StatusToColor, BoolToVisibility, MessageTypeToStyle)
    status: completed
  - id: main-layout
    content: Build MainWindow.axaml with Grid+GridSplitter 4-panel layout (Chat, Agents, Todos, ToolCalls), menu bar, status bar, and dark theme
    status: completed
  - id: chat-view
    content: Build ChatView.axaml with ScrollViewer+ItemsControl for messages, Markdown rendering via Avalonia.Controls.Markdown, MessageBubble UserControl with role-based styling
    status: completed
  - id: input-view
    content: Build input area with multi-line TextBox, Enter/Shift+Enter handling, input history, and send button
    status: completed
  - id: agent-panel
    content: Build AgentPanelView.axaml with ListBox of agents showing type badges, status icons (spinning/check/cross), elapsed time, and click-to-expand detail
    status: completed
  - id: todo-panel
    content: Build TodoPanelView.axaml with ItemsControl for todos, status indicators (checkbox styles for pending/in_progress/completed/cancelled), progress bar
    status: completed
  - id: toolcall-panel
    content: Build ToolCallPanelView.axaml with scrolling feed of tool calls, expandable detail (arguments/result), duration display, status coloring
    status: completed
  - id: viewmodels
    content: Create all ViewModels (MainWindowVM, ChatVM, AgentPanelVM, TodoPanelVM, ToolCallPanelVM) with CommunityToolkit.Mvvm ObservableProperty and RelayCommand
    status: completed
  - id: adapters
    content: "Implement 4 Avalonia adapters: AvaloniaOutputSink, AvaloniaUserInteraction, AvaloniaProgressReporter, AvaloniaToolCallObserver - all with Dispatcher.UIThread marshalling"
    status: completed
  - id: dialogs
    content: Build QuestionDialog.axaml (single/multi-select overlay) and SetupWizardDialog.axaml (multi-step provider config) with async TaskCompletionSource flow
    status: completed
  - id: app-lifecycle
    content: "Wire App.axaml.cs bootstrap: load .env, provider config, setup wizard, EngineBuilder, connect adapters, handle slash commands, process agent messages"
    status: completed
  - id: theme-polish
    content: Apply Semi.Avalonia dark theme, custom styles for message bubbles, status colors, monospace code fonts, notification toasts, keyboard shortcuts
    status: completed
isProject: false
---

# Avalonia Cross-Platform UI for MiniClaudeCode

## PRD (Product Requirements Document)

### P0 - Core Chat Interface

- **Chat Panel**: Markdown-rendered message area with user/assistant/system message bubbles, auto-scroll, code block syntax highlighting
- **Input Area**: Multi-line text input with Enter to send, Shift+Enter for newline, input history (up/down arrows)
- **Streaming Response**: Real-time token-by-token display as LLM responds
- **Slash Commands**: /help, /new, /clear, /status, /model, /compact, /exit - same as TUI

### P0 - Sub-Agent System

- **Agent Panel**: Real-time list of running/completed/failed sub-agents with status indicators (spinning, checkmark, cross)
- **Parallel Exploration**: Visual display of multiple concurrent agents with individual progress bars
- **Agent Detail View**: Click an agent to see its full output/result in a detail pane
- **Agent Type Badges**: Visual badges for generalPurpose, explore, code, plan agent types

### P0 - Tool Call Visualization

- **Tool Call Panel**: Live feed of tool invocations with name, arguments preview, duration, status (running/success/fail)
- **Tool Call Detail**: Expandable tool call entries showing full arguments and return values
- **Tool Call Counters**: Running count in title bar / status bar

### P0 - Todo/Plan Management

- **Todo Panel**: Structured task list with status indicators (pending, in_progress, completed, cancelled)
- **Real-time Updates**: Todos update live as the agent creates/modifies them
- **Visual Progress**: Progress bar showing completed/total ratio

### P0 - Ask Question / User Interaction

- **Question Dialog**: Modal overlay for agent-initiated questions with structured options
- **Multi-Select Support**: Checkbox-style selection for allow_multiple questions
- **Confirmation Dialog**: Yes/No confirmation dialogs for destructive operations
- **Secret Input**: Password-masked input for API keys

### P1 - Rules & Skills

- **Rules Indicator**: Status bar indicator showing loaded rules count
- **Skills Browser**: View available skills and their descriptions
- **Rule Viewer**: View loaded rules content on demand

### P1 - Setup & Configuration

- **Setup Wizard**: First-run dialog for API key configuration (OpenAI, DeepSeek, Zhipu)
- **Provider Switcher**: Dropdown to switch active provider without restart
- **Model Selector**: Choose model within the active provider

### P1 - Session Management

- **New Conversation**: Reset thread with confirmation
- **Compact Conversation**: Summarize and compress long conversations
- **Status Dashboard**: Turn count, tool calls, agent count, provider info

### P2 - Enhanced UX

- **Dark/Light Theme Toggle**: Semi.Avalonia theme with runtime switching
- **Notification Toasts**: Non-blocking notifications for agent events
- **Keyboard Shortcuts**: Full keyboard navigation (Ctrl+N new, Ctrl+L clear, etc.)
- **Panel Resizing**: Draggable GridSplitters between all panels
- **Responsive Layout**: Panels collapse gracefully on smaller windows

---

## Architecture

### Project Structure

```
csharp/src/MiniClaudeCode.Avalonia/
├── MiniClaudeCode.Avalonia.csproj
├── Program.cs                          # Entry point
├── App.axaml / App.axaml.cs           # Avalonia Application
├── Assets/
│   ├── Fonts/                          # Custom fonts (monospace for code)
│   └── Icons/                          # SVG icons for status indicators
├── Converters/
│   ├── StatusToColorConverter.cs       # Agent/todo status -> color
│   ├── BoolToVisibilityConverter.cs
│   └── MessageTypeToStyleConverter.cs
├── Models/
│   ├── ChatMessage.cs                  # UI chat message model
│   ├── ToolCallItem.cs                 # UI tool call model
│   └── TodoItem.cs                     # UI todo item model
├── ViewModels/
│   ├── MainWindowViewModel.cs          # Root VM, orchestrates all panels
│   ├── ChatViewModel.cs               # Chat messages + input logic
│   ├── AgentPanelViewModel.cs         # Agent status tracking
│   ├── TodoPanelViewModel.cs          # Todo list management
│   ├── ToolCallPanelViewModel.cs      # Tool call feed
│   ├── SetupWizardViewModel.cs        # Provider configuration
│   └── QuestionDialogViewModel.cs     # Ask question interaction
├── Views/
│   ├── MainWindow.axaml / .cs         # Root layout with GridSplitters
│   ├── ChatView.axaml / .cs           # Chat panel (messages + input)
│   ├── AgentPanelView.axaml / .cs     # Agent status panel
│   ├── TodoPanelView.axaml / .cs      # Todo list panel
│   ├── ToolCallPanelView.axaml / .cs  # Tool call history
│   ├── MessageBubble.axaml / .cs      # Individual chat message control
│   ├── SetupWizardDialog.axaml / .cs  # Setup wizard overlay
│   └── QuestionDialog.axaml / .cs     # Agent question overlay
├── Adapters/
│   ├── AvaloniaOutputSink.cs          # IOutputSink -> ChatViewModel
│   ├── AvaloniaUserInteraction.cs     # IUserInteraction -> dialogs
│   ├── AvaloniaProgressReporter.cs    # IAgentProgressReporter -> AgentPanelVM
│   └── AvaloniaToolCallObserver.cs    # IToolCallObserver -> ToolCallPanelVM
├── Services/
│   └── DispatcherService.cs           # Thread marshalling helper
└── Themes/
    └── AppTheme.axaml                 # Custom style overrides
```

### Key Design Decisions

1. **MVVM with CommunityToolkit.Mvvm** - Source generators for `ObservableProperty`, `RelayCommand`, messaging
2. **Semi.Avalonia Theme** - Modern dark theme with built-in dark/light variants
3. **Avalonia.Controls.Markdown** - Native markdown rendering in chat messages
4. **Reuse all Abstractions interfaces** - Same 4 UI contracts: `IOutputSink`, `IUserInteraction`, `IAgentProgressReporter`, `IToolCallObserver`
5. **Dispatcher-safe adapters** - All adapter implementations marshal to UI thread via `Dispatcher.UIThread.Post()`
6. **Same EngineBuilder flow** - Identical engine construction as TUI, just different adapter implementations

### Adapter Mapping (reuses [MiniClaudeCode.Abstractions](csharp/src/MiniClaudeCode.Abstractions/))

| Interface | Avalonia Implementation | Target |

|---|---|---|

| `IOutputSink` | `AvaloniaOutputSink` | Pushes to `ChatViewModel.Messages` |

| `IUserInteraction` | `AvaloniaUserInteraction` | Shows overlay dialogs, returns via TaskCompletionSource |

| `IAgentProgressReporter` | `AvaloniaProgressReporter` | Updates `AgentPanelViewModel.Agents` collection |

| `IToolCallObserver` | `AvaloniaToolCallObserver` | Appends to `ToolCallPanelViewModel.ToolCalls` |

### NuGet Dependencies

- `Avalonia` (11.x) - Core framework
- `Avalonia.Desktop` (11.x) - Desktop platform support
- `Avalonia.Themes.Fluent` (11.x) - Fallback theme
- `Semi.Avalonia` - Modern dark theme
- `CommunityToolkit.Mvvm` - MVVM infrastructure
- `Avalonia.Controls.Markdown` - Markdown rendering (Avalonia Accelerate)
- `DotNetEnv` - Environment file loading

---

## UI Layout Design

### Main Window Layout (Dark Theme)

````
+----------------------------------------------------------------------+
| [Menu: File | View | Help]                    MiniClaudeCode v0.3.0  |
+----------------------------------------------------------------------+
|                                    |  [Agents]            [30%]      |
|  [Chat Area - 65%]                |  > explore-1  Running...  0:03  |
|                                    |  v gp-2       Done        0:12  |
|  [System] Welcome to MiniClaudeCode|  x code-3     Failed      0:01  |
|                                    |                                  |
|  [You] How does auth work?         +----------------------------------+
|                                    |  [Todos]              [30%]      |
|  [Assistant] Let me explore...     |  [x] Setup auth module           |
|  I found the auth flow in          |  [>] Implement login      <<<   |
|  `src/auth/`:                      |  [ ] Add tests                   |
|  ```typescript                     |  [ ] Update docs                 |
|  export class AuthService {        |  --------- 1/4 (25%) ----------  |
|    ...                             +----------------------------------+
|  ```                               |  [Tool Calls]          [40%]     |
|                                    |  > grep "AuthService" (0.3s)     |
|                                    |  > read_file auth.ts  (0.1s)     |
|                                    |  * glob "**/*.test.*" running...  |
|                                    |                                  |
+------------------------------------+----------------------------------+
| [Input Area]                       |                                  |
| Type message... (Enter=send)       |  Status: OpenAI | T:3 | TC:12   |
+------------------------------------+----------------------------------+
````

### Question Dialog Overlay

```
+----------------------------------------------------------------------+
|                                                                      |
|           +------------------------------------------+               |
|           |  Agent Question                          |               |
|           |                                          |               |
|           |  Which implementation do you prefer?     |               |
|           |                                          |               |
|           |  ( ) Option A: Repository pattern        |               |
|           |  (*) Option B: Service layer pattern     |               |
|           |  ( ) Option C: Direct data access        |               |
|           |                                          |               |
|           |           [Cancel]  [Confirm]             |               |
|           +------------------------------------------+               |
|                                                                      |
+----------------------------------------------------------------------+
```

### Setup Wizard Dialog

```
+----------------------------------------------------------------------+
|           +------------------------------------------+               |
|           |  Setup Wizard                    Step 1/3|               |
|           |                                          |               |
|           |  Select your AI provider:                |               |
|           |                                          |               |
|           |  [v] OpenAI                              |               |
|           |  [ ] DeepSeek                            |               |
|           |  [ ] Zhipu GLM                           |               |
|           |                                          |               |
|           |  API Key: [***************]              |               |
|           |  Model:   [gpt-4o          v]            |               |
|           |                                          |               |
|           |           [Back]   [Next >]               |               |
|           +------------------------------------------+               |
+----------------------------------------------------------------------+
```

---

## Implementation Order

The project reuses all existing `MiniClaudeCode.Core` and `MiniClaudeCode.Abstractions` libraries unchanged. Only new files are created in the `MiniClaudeCode.Avalonia` project.
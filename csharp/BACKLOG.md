# Avalonia VS Code Workbench Backlog (MVP -> V1)

## Milestones

1. MVP
- Single process implementation with logical 3-layer boundaries.
- Single editor group.
- Open/Edit/Dirty/Save/Close core file flow.
- Explorer lazy tree + file open.
- External file change refresh (non-conflict baseline).
- Command palette baseline (register/search/execute).

2. V1
- Multi-group editors + drag/drop tabs.
- Preview tab lifecycle.
- Save conflict detection + resolution dialog.
- Backup/restore on save failure and crash recovery.
- IPC split to 3-process runtime.
- Dark+ visual fidelity pass.

## Epic 1: Foundations and Shell Layout

### Story 1.1 Workbench Shell and Regions
Tasks:
- Create 5-region layout: ActivityBar, SideBar, EditorArea, Panel, StatusBar.
- Add splitter constraints (SideBar min 170, EditorGroup min 220, Panel min 140).
- Add layout state persistence (window restore).

DoD:
- Regions render correctly on Win/Linux/macOS.
- Resize constraints are enforced.
- Layout state survives restart.

Test template:
- Given app starts fresh, when user resizes SideBar and Panel, then values are restored after restart.

### Story 1.2 Theme Token Infrastructure (Dark+)
Tasks:
- Define semantic token map (`--vscode-*` equivalent keys).
- Bind controls to semantic tokens instead of hardcoded colors.
- Add typography baseline for editor/UI.

DoD:
- All shell regions consume semantic tokens.
- Theme switch path exists even if only Dark+ is shipped now.

Test template:
- Given Dark+ theme, when app renders, then shell colors match token mappings without hardcoded overrides.

## Epic 2: File Service and Sandbox Workspace

### Story 2.1 Workspace Sandbox Guard
Tasks:
- Implement workspace root guard for all file ops.
- Normalize path casing/separators per OS before authorization checks.
- Return typed errors (`OutOfWorkspace`, `NotFound`, `AccessDenied`).

DoD:
- Any path escape attempt is blocked.
- Symlink handling is deterministic and documented.

Test template:
- Given workspace root, when user attempts `..` traversal, then operation is rejected with `OutOfWorkspace`.

### Story 2.2 IFileService Core Ops
Tasks:
- Implement `ResolveAsync`, `ResolveContentAsync`, `WriteFileAsync`.
- Add atomic write strategy (`temp + flush + rename`).
- Add ETag/mtime based stale-write detection hooks.

DoD:
- Read/write works for UTF-8 text files.
- Atomic write path is used for all saves.
- Stale-write detection data is available to upper layers.

Test template:
- Given file exists, when write succeeds, then file content updates atomically and no partial content is observed.

### Story 2.3 Watcher Pipeline Baseline
Tasks:
- Implement watcher backend abstraction.
- Add event coalescing window (100-300ms).
- Normalize and deduplicate events by canonical resource key.

DoD:
- File add/update/delete events arrive in stable order.
- Event storms do not freeze UI.

Test template:
- Given bulk rename in folder, when watcher emits events, then Explorer refreshes once per coalesced batch.

## Epic 3: Explorer Tree

### Story 3.1 Virtualized Lazy Tree
Tasks:
- Implement `ExplorerNode` model and on-demand children loading.
- Add virtualization for large directories.
- Add incremental refresh for affected subtrees only.

DoD:
- Tree supports large workspace browsing without full materialization.
- Expand/collapse latency remains acceptable under large node counts.

Test template:
- Given 50k-node workspace, when user scrolls and expands folders, then UI remains responsive and memory stable.

### Story 3.2 Explorer Actions
Tasks:
- Add context menu: rename/delete/new file/new folder/reveal.
- Implement DnD move/copy with overwrite confirmation.
- Ensure action command routing goes through command service.

DoD:
- Explorer actions update both FS and tree state correctly.
- Error states are surfaced through notifications/status bar.

Test template:
- Given a file rename action, when name conflicts, then conflict prompt appears and user choice is respected.

## Epic 4: Text Model and Editor Lifecycle

### Story 4.1 TextFileModel State Machine
Tasks:
- Implement `VersionId`, `SavedVersionId`, `Dirty`, `Orphaned`, `Encoding`, `ETag`.
- Define `FileState` transitions (`Saved/Dirty/Conflict/Orphan/Error`).
- Broadcast `model.dirtyChanged` events.

DoD:
- Dirty state is deterministic across edit/save/revert.
- State transitions are logged and testable.

Test template:
- Given clean file, when edit occurs, then `Dirty=true` and `VersionId` increments.

### Story 4.2 AvaloniaEdit Binding
Tasks:
- Build bidirectional binding between `TextFileModel` and AvaloniaEdit buffer.
- Add cursor/selection state persistence on reopen.
- Prevent reentrant update loops.

DoD:
- Editor text always reflects active model.
- Model changes from service side update view safely.

Test template:
- Given external model update, when editor is active and clean, then content refreshes without duplicate change events.

### Story 4.3 EditorService Open/Close Rules
Tasks:
- Implement `OpenEditorAsync` activation semantics.
- Prevent duplicate model creation for same resource.
- Implement close flow with dirty checks and prompts.

DoD:
- Reopening same file reuses existing editor input/model.
- Dirty close prompt supports Save / Don’t Save / Cancel.

Test template:
- Given same file open request twice, when second request executes, then existing tab is focused without new model creation.

## Epic 5: Tabs, Groups, and Preview Mode

### Story 5.1 Tab and Preview Lifecycle
Tasks:
- Implement preview tab replacement semantics.
- Convert preview to pinned on double click or edit.
- Render dirty indicator and readonly state.

DoD:
- Preview behavior matches defined UX rules.
- Pinned tabs are not replaced by new preview opens.

Test template:
- Given preview tab open, when user opens another file in preview mode, then first preview is replaced.

### Story 5.2 Multi-Group Layout and DnD
Tasks:
- Implement group create/split (`left/right/up/down`).
- Support tab reorder in-group and move across groups.
- Support drag-out to create new group.

DoD:
- Group and tab operations preserve active editor correctness.
- DnD works with keyboard fallback commands.

Test template:
- Given two groups, when tab is dragged across groups, then file remains open and active state transfers correctly.

## Epic 6: Save, Backup, Conflict

### Story 6.1 Save/Revert/SaveAll
Tasks:
- Implement save commands pipeline with `OnWillSave`.
- Add batch SaveAll with failure isolation.
- Add Revert with model and view synchronization.

DoD:
- Manual save path is stable and deterministic.
- Save failures do not corrupt dirty models.

Test template:
- Given multiple dirty files, when SaveAll runs and one save fails, then other files still save successfully.

### Story 6.2 Backup and Crash Recovery
Tasks:
- Define backup key (`workspace + resource`) and storage path.
- Write backup before risky save operations.
- Implement startup recovery workflow and cleanup policy.

DoD:
- Failed save can restore from backup.
- Crash restart offers valid recovery options.

Test template:
- Given crash during unsaved edits, when app restarts, then recovery prompt shows and restore succeeds.

### Story 6.3 External Change Conflict Handling
Tasks:
- Compare in-memory state with external ETag/mtime changes.
- Auto-reload clean models.
- Prompt conflict flow for dirty models (reload/compare/keep).

DoD:
- Clean files refresh automatically.
- Dirty files never silently lose changes.

Test template:
- Given dirty open file and external write, when watcher event arrives, then conflict dialog appears with correct options.

## Epic 7: Command System and Keybindings

### Story 7.1 Command Registry and Context Keys
Tasks:
- Implement command registry with metadata (`id`, title, category, when).
- Add context key service and `when` expression evaluator.
- Add enablement/visibility checks for menus and palette.

DoD:
- Commands resolve by current context.
- Hidden/disabled commands are filtered correctly.

Test template:
- Given command with `when=editorFocus`, when focus leaves editor, then command no longer appears executable.

### Story 7.2 Command Palette
Tasks:
- Implement palette UI with fuzzy search and scoring.
- Show command title/category/keybinding.
- Add keyboard navigation and execute-on-enter.

DoD:
- `Ctrl/Cmd+Shift+P` opens palette globally.
- Top search result quality is acceptable for common commands.

Test template:
- Given palette open, when typing partial command text, then relevant command ranks in top results and executes on Enter.

## Epic 8: Process Split and IPC

### Story 8.1 In-Process Contracts First
Tasks:
- Finalize service contracts/envelopes in-process.
- Add telemetry around command latency and event volume.
- Define high-frequency events that stay local (non-IPC).

DoD:
- Contract tests pass before out-of-process split.
- No editor keystroke-level traffic in IPC plan.

Test template:
- Given typing in editor, when profiling events, then no per-keystroke IPC messages are emitted.

### Story 8.2 Out-of-Process Migration
Tasks:
- Move Core Service to separate process (gRPC over pipe/UDS).
- Move Watcher/Extension shell to separate process.
- Add reconnection and degraded-mode behavior.

DoD:
- Process crash in watcher/core is recoverable without full app restart where feasible.
- Command and event channels remain backward compatible.

Test template:
- Given watcher process restart, when reconnection completes, then Explorer continues receiving file events.

## Cross-Cutting NFRs

Tasks:
- Define performance budgets (cold start, first paint, tree expand latency, memory P95).
- Add structured logs and trace IDs (`requestId`, `channel`, `command`).
- Add fault injection tests for IO and IPC failures.

DoD:
- Performance and reliability metrics are measurable in CI or nightly runs.
- Regression thresholds are versioned and enforced.

## Backlog Prioritization

1. P0 (MVP critical)
- Epic 1, Story 2.1/2.2, Story 3.1, Story 4.1/4.2/4.3, Story 6.1, Story 7.1/7.2 (baseline), Story 8.1.

2. P1 (V1 critical)
- Story 5.1/5.2, Story 6.2/6.3, Story 2.3, Story 8.2, Dark+ full pass.

3. P2 (post-V1)
- Multi-root workspace, richer compare UX, extension-host actual compatibility.

## Suggested Sprint Slice (2-week cadence)

1. Sprint A
- Epic 1 + Story 2.1 + Story 2.2 + Story 4.1 baseline.

2. Sprint B
- Story 3.1 + Story 4.2 + Story 4.3 + Story 6.1.

3. Sprint C
- Story 5.1 + Story 5.2 + Story 7.1 + Story 7.2.

4. Sprint D
- Story 2.3 + Story 6.2 + Story 6.3 + Epic 8.2.


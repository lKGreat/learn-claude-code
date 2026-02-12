---
name: Implement EXPLORER Module
overview: Strictly follow the VSCode architecture document to fill the gaps in the MiniClaudeCode.Avalonia EXPLORER module by adding service abstractions (IExplorerService, ITextFileService), enhancing data models (FileTreeNode, EditorTab, ExplorerViewState, FileStat), implementing missing features (drag & drop, multi-select, version tracking, hash-based conflict detection), and refining the file open/save/close flows.
todos:
  - id: models-foundation
    content: "Create foundation models: FileStat, ExplorerEvent (enum + record), ExplorerViewState, TextFileModel"
    status: completed
  - id: enhance-filetreenode
    content: "Enhance FileTreeNode: add Id, Parent reference, IsSelected property"
    status: completed
  - id: enhance-editortab
    content: "Enhance EditorTab: add SavedVersionId / CurrentVersionId, populate LastKnownDiskHash with SHA256"
    status: completed
  - id: explorer-service
    content: Create IExplorerService interface + ExplorerService implementation (extract file tree logic from ViewModel)
    status: completed
  - id: textfile-service
    content: Create ITextFileService interface + TextFileService implementation (model caching, reference counting, save/revert)
    status: completed
  - id: refactor-fileexplorer-vm
    content: Refactor FileExplorerViewModel to delegate to ExplorerService, add multi-select, state persistence
    status: completed
  - id: refactor-editor-vm
    content: Refactor EditorViewModel to use TextFileService for open/save/close flows, implement version-based dirty tracking and hash-based conflict detection
    status: completed
  - id: drag-drop
    content: Add drag & drop support in FileExplorerView (AXAML + code-behind) with MoveNode logic
    status: completed
  - id: wire-mainwindow
    content: Wire ExplorerService and TextFileService in MainWindowViewModel, connect FileWatcher events
    status: completed
  - id: build-verify
    content: Build the solution (dotnet build csharp/MiniClaudeCode.slnx), fix any compilation errors and lint issues
    status: completed
isProject: false
---

# Implement EXPLORER Module Per VSCode Architecture Doc

## Current State Analysis

The project already has a functional EXPLORER implementation with:

- `FileTreeNode` model, `FileExplorerViewModel`, `FileExplorerView` (AXAML + code-behind)
- `EditorTab` with `FileState` enum, dirty tracking, preview mode
- `EditorViewModel` with open/preview/close/save, conflict resolution
- `FileOperationsViewModel`, `FileWatcherService`, `FileBackupService`
- `BoolToFontStyleConverter` for preview tab italic styling
- `MainWindowViewModel` wiring everything together

## Gaps to Fill (Per Document Sections)

### Gap 1: Service Abstractions (Doc Section 3 + 4.1)

The document defines clear **service layers** between UI and file system. Currently, the ViewModel directly interacts with the file system. We need to introduce:

**New file: `Services/Explorer/IExplorerService.cs`** -- Interface for file tree data management, node state maintenance, and event dispatching. Maps to VSCode's `ExplorerService` (doc 4.1).

```csharp
public interface IExplorerService
{
    ObservableCollection<FileTreeNode> RootNodes { get; }
    FileTreeNode? FocusedNode { get; set; }
    IReadOnlySet<string> ExpandedNodeIds { get; }
    void LoadWorkspace(string path);
    void LoadChildren(FileTreeNode node);
    void SetSelectedNodes(IEnumerable<FileTreeNode> nodes);
    void ToggleNodeExpansion(FileTreeNode node);
    void RefreshNode(FileTreeNode node);
    event Action<ExplorerEvent>? EventFired;
}
```

**New file: `Services/Explorer/ExplorerService.cs`** -- Implementation that extracts file tree logic from `FileExplorerViewModel`. The ViewModel becomes a thin coordinator that delegates to this service.

**New file: `Services/Explorer/ITextFileService.cs`** -- Interface for text file model management with caching, dirty tracking, and save semantics. Maps to VSCode's `ITextFileService` (doc 3.3).

```csharp
public interface ITextFileService
{
    event Action<TextFileModelChangeEvent>? OnDidChangeDirty;
    event Action<TextFileSaveEvent>? OnDidSave;
    event Action<TextFileLoadEvent>? OnDidLoad;
    TextFileModel? GetModel(string filePath);
    Task<TextFileModel> ResolveAsync(string filePath);
    Task<bool> SaveAsync(string filePath, SaveOptions? options = null);
    Task<bool> RevertAsync(string filePath);
    IReadOnlyList<string> GetDirtyFiles();
}
```

**New file: `Services/Explorer/TextFileService.cs`** -- Implementation with model caching and reference counting for disposal (doc 6.3 TextModelService pattern).

### Gap 2: Enhanced FileTreeNode Model (Doc Section 4.2 - IExplorerNode)

**Modify: [`Models/FileTreeNode.cs`](csharp/src/MiniClaudeCode.Avalonia/Models/FileTreeNode.cs)**

Add missing properties from the document's `IExplorerNode`:

- `Id` (string) -- unique identifier, use full path as key
- `Parent` (FileTreeNode?) -- parent reference for navigation
- `IsSelected` (bool) -- observable property for multi-selection UI
- `Resource` (string) -- alias for FullPath, matching doc naming

Currently `Name` and `FullPath` are `init`-only, which prevents rename. Change to support mutation via `ObservableProperty` or new node creation.

### Gap 3: ExplorerViewState (Doc Section 4.2)

**New file: `Models/ExplorerViewState.cs`**

```csharp
public class ExplorerViewState
{
    public IReadOnlyList<FileTreeNode> RootNodes { get; set; }
    public IReadOnlyList<FileTreeNode> SelectedNodes { get; set; }
    public HashSet<string> ExpandedNodeIds { get; set; }
    public FileTreeNode? FocusedNode { get; set; }
}
```

Used by `IExplorerService` for state persistence/restoration (e.g., re-expand previously expanded folders after refresh).

### Gap 4: Explorer Event System (Doc Section 4.4)

**New file: `Models/ExplorerEvent.cs`**

```csharp
public enum ExplorerEventType
{
    NodeClick, NodeDoubleClick, NodeContextMenu,
    NodeCollapse, NodeExpand, NodeRename, NodeDelete,
    NodeDragStart, NodeDrop
}

public record ExplorerEvent(ExplorerEventType Type, FileTreeNode Node, FileTreeNode? TargetNode = null);
```

This formalizes the event dispatching described in doc section 4.4. The `IExplorerService` fires these events, and the `FileExplorerViewModel` subscribes.

### Gap 5: Drag & Drop Support (Doc Section 4.4)

**Modify: [`Views/FileExplorerView.axaml`](csharp/src/MiniClaudeCode.Avalonia/Views/FileExplorerView.axaml)**

- Add `DragDrop.AllowDrop`, `PointerPressed`, `DragOver`, `Drop` event handlers on TreeView items

**Modify: [`Views/FileExplorerView.axaml.cs`](csharp/src/MiniClaudeCode.Avalonia/Views/FileExplorerView.axaml.cs)**

- Implement drag & drop handlers for file/folder reordering
- Fire `NodeDragStart` / `NodeDrop` events via ExplorerService

**Modify: `FileOperationsViewModel.cs` or `ExplorerService`**

- Add `MoveNode(FileTreeNode source, FileTreeNode targetParent)` for the actual file system move

### Gap 6: IFileStat Model (Doc Section 9.5)

**New file: `Models/FileStat.cs`**

```csharp
public record FileStat(
    string Resource,     // full path
    string Name,
    bool IsFile,
    bool IsDirectory,
    bool IsSymbolicLink,
    long Size,
    DateTime ModifiedTime,
    DateTime CreatedTime,
    FileStat[]? Children = null);
```

Used by `IExplorerService` and `ITextFileService` for file metadata. This replaces ad-hoc `FileInfo` usage scattered through the codebase.

### Gap 7: File State Tracking Enhancement (Doc Section 5)

**Modify: [`Models/EditorTab.cs`](csharp/src/MiniClaudeCode.Avalonia/Models/EditorTab.cs)**

Add version tracking fields from doc section 5.3:

```csharp
// Version-based dirty detection (doc 5.3)
public int SavedVersionId { get; set; } = 0;
public int CurrentVersionId { get; set; } = 0;
```

**Modify: [`ViewModels/EditorViewModel.cs`](csharp/src/MiniClaudeCode.Avalonia/ViewModels/EditorViewModel.cs)**

- Implement `UpdateContent()` to increment `CurrentVersionId` and derive dirty state from `CurrentVersionId != SavedVersionId` (doc 5.3)
- Implement hash-based content comparison in `HandleExternalFileChange()` -- the `LastKnownDiskHash` field exists but is never populated. Use SHA256 to set it on load/save, and compare on external change (doc 5.4)
- Add `OnWillSave` event hook before writing (doc 7.2 step 3)

### Gap 8: TextFileModel (Doc Section 9.2 - ITextFileEditorModel)

**New file: `Models/TextFileModel.cs`**

```csharp
public class TextFileModel : ObservableObject
{
    public string Resource { get; }
    public string Content { get; set; }
    public string Encoding { get; set; } = "utf-8";
    public bool IsDirty => CurrentVersionId != SavedVersionId;
    public bool IsResolved { get; set; }
    public bool IsOrphaned { get; set; }
    public int SavedVersionId { get; set; }
    public int CurrentVersionId { get; set; }
    public string? DiskHash { get; set; }
    public DateTime? DiskModifiedTime { get; set; }
    public PieceTreeTextBuffer? TextBuffer { get; set; }
    public int ReferenceCount { get; set; }
    
    public event Action? OnDidChangeContent;
    public event Action? OnDidChangeDirty;
    public event Action? OnDidSave;
}
```

This is the in-memory representation per-file, cached by `TextFileService`. `EditorTab` delegates to it for state. Reference counting ensures model is disposed when no tab references it (doc 8.1 step 4).

### Gap 9: Refactor FileExplorerViewModel (Doc Section 4.1)

**Modify: [`ViewModels/FileExplorerViewModel.cs`](csharp/src/MiniClaudeCode.Avalonia/ViewModels/FileExplorerViewModel.cs)**

- Extract file system logic into `ExplorerService`
- ViewModel becomes a thin coordinator: delegates to `IExplorerService`, subscribes to events, updates UI state
- Add `SelectedNodes` collection for multi-select
- Persist/restore `ExplorerViewState` on workspace load

### Gap 10: Refactor EditorViewModel Save/Open Flow (Doc Sections 6, 7, 8)

**Modify: [`ViewModels/EditorViewModel.cs`](csharp/src/MiniClaudeCode.Avalonia/ViewModels/EditorViewModel.cs)**

- Integrate `ITextFileService` for file operations instead of direct `File.ReadAllText`/`File.WriteAllText`
- `OpenFile` flow: check cache -> resolve via TextFileService -> create/reuse TextFileModel -> update tab (doc 6.1)
- `SaveFile` flow: fire OnWillSave -> backup -> write via TextFileService -> update state -> fire OnDidSave (doc 7.2)
- `CloseTab` flow: check dirty -> confirm -> dispose model if refcount=0 -> fire events (doc 8.1)
- Move model caching from EditorViewModel into TextFileService

### Gap 11: Wire Up in MainWindowViewModel

**Modify: [`ViewModels/MainWindowViewModel.cs`](csharp/src/MiniClaudeCode.Avalonia/ViewModels/MainWindowViewModel.cs)**

- Create `ExplorerService` and `TextFileService` instances
- Inject them into `FileExplorerViewModel` and `EditorViewModel`
- Wire `FileWatcherService` events to `ExplorerService` refresh and `TextFileService` conflict detection (doc 10.3)

## Implementation Order

The tasks below are ordered by dependency -- earlier tasks produce types consumed by later tasks.

## Files Summary

New files to create (7):

- `Services/Explorer/IExplorerService.cs`
- `Services/Explorer/ExplorerService.cs`
- `Services/Explorer/ITextFileService.cs`
- `Services/Explorer/TextFileService.cs`
- `Models/ExplorerViewState.cs`
- `Models/ExplorerEvent.cs`
- `Models/TextFileModel.cs`
- `Models/FileStat.cs`

Files to modify (7):

- `Models/FileTreeNode.cs` -- add Id, Parent, IsSelected
- `Models/EditorTab.cs` -- add version tracking, delegate to TextFileModel
- `ViewModels/FileExplorerViewModel.cs` -- refactor to use ExplorerService
- `ViewModels/EditorViewModel.cs` -- refactor to use TextFileService, version tracking, hash-based conflict, OnWillSave
- `ViewModels/MainWindowViewModel.cs` -- wire up new services
- `Views/FileExplorerView.axaml` -- add drag & drop attributes
- `Views/FileExplorerView.axaml.cs` -- add drag & drop handlers
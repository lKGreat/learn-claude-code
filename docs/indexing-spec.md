# Codebase Indexing System - Technical Specification

**Project**: MiniClaudeCode
**Version**: 1.0
**Date**: 2026-02-12
**Status**: Draft

---

## 1. Requirements Summary

MiniClaudeCode requires a codebase indexing system that enables:
- **Fast file search** with fuzzy matching (like VS Code Ctrl+P)
- **Symbol extraction** from C#, TypeScript, Python, and other languages
- **@ mention completion** in chat for referencing files and symbols
- **Context building** for AI by identifying relevant files based on usage patterns

The system must integrate with existing components:
- `FileWatcherService.cs` for incremental updates
- `FileSearchPlugin.cs` for glob pattern matching
- `WorkspaceService.cs` for multi-root workspace support

---

## 2. Clarifying Questions

The following assumptions are made. If these are incorrect, the specification should be updated:

1. **Index persistence**: Should the index persist to disk for faster startup, or rebuild on every workspace open?
   **Assumption**: Rebuild on startup (simpler MVP), with in-memory-only storage. Future iteration can add SQLite persistence.

2. **Symbol detail level**: Should we extract parameter types, return types, visibility modifiers?
   **Assumption**: Extract name, kind, line/column, signature (full text), parent symbol. Detailed parsing (parameter lists) is future work.

3. **Import analysis depth**: Should we resolve transitive dependencies (imports of imports)?
   **Assumption**: Track direct imports only for MVP. Transitive analysis is future work.

4. **Multi-language priority**: Which languages are P0 (must-have) vs. P1 (nice-to-have)?
   **Assumption**: P0 = C#, TypeScript. P1 = Python, JavaScript. P2 = Rust, Go, Java.

5. **Thread safety**: Will the index be accessed from multiple threads (e.g., UI thread + background indexer)?
   **Assumption**: Yes. Use `ReaderWriterLockSlim` for concurrent read access during search, exclusive write during updates.

---

## 3. Multi-Dimension Analysis

### 3.1 Functional Dimension (功能维度)

#### 3.1.1 File Index
**Feature**: Maintain an index of all workspace files with metadata.

**Capabilities**:
- Store file path (relative to workspace root), absolute path, language ID, file size, last modified timestamp, content hash (SHA256 of first 4KB for change detection)
- Detect language from file extension mapping (`.cs` → `csharp`, `.ts` → `typescript`, `.tsx` → `typescriptreact`, `.py` → `python`, `.js` → `javascript`, `.rs` → `rust`, `.go` → `go`, `.java` → `java`)
- Filter files based on ignore patterns (`.git/`, `node_modules/`, `bin/`, `obj/`, `.vs/`, `.idea/`, `__pycache__/`, `.venv/`, `.next/`, `dist/`, `build/`, `.cursor/`, `coverage/`, `.nyc_output/`)
- Support multi-root workspaces (track which workspace folder each file belongs to)

**Input/Output**:
- Input: Workspace folder paths (from `WorkspaceService`)
- Output: `FileIndexEntry` collection with fast lookup by path and language

**Boundary Conditions**:
- Maximum file size indexed: 10MB (files larger than this are tracked but not indexed for symbols)
- Maximum total files: 100,000 per workspace
- Binary files (`.dll`, `.exe`, `.png`, `.jpg`, etc.): tracked in file index but not parsed for symbols
- Symlinks: Follow symlinks but detect cycles (stop after 5 levels)

**Dependencies**:
- `WorkspaceService.Folders` for workspace root paths
- `FileWatcherService` for change notifications

**Exclusions**:
- Does not index file content (full-text search is out of scope for MVP)
- Does not track file permissions or ownership
- Does not index files outside workspace folders

#### 3.1.2 Symbol Index
**Feature**: Extract and store code symbols (classes, methods, properties, etc.) from source files.

**Capabilities**:
- Extract symbols using regex patterns (detailed in Section 4)
- Store symbol name, kind (enum: `Class`, `Interface`, `Method`, `Property`, `Function`, `Enum`, `Struct`, `Field`, `Const`, `Type`), file path, line number, column number, full signature text, parent symbol reference
- Build parent-child hierarchy (e.g., `MyClass.MyMethod` → parent is `MyClass`)
- Support multiple symbols with same name in different files/scopes
- Track symbol visibility (public/private/protected) for C# and TypeScript

**Input/Output**:
- Input: File path, file content (string), language ID
- Output: `SymbolIndexEntry[]` array for that file

**Boundary Conditions**:
- Maximum symbols per file: 10,000 (files with more symbols are partially indexed with warning logged)
- Minimum symbol name length: 1 character
- Maximum symbol signature length: 500 characters (truncated if longer)
- Handle malformed code gracefully (regex failures should not crash indexing)

**Dependencies**:
- File index (to know which files to parse)
- Language detection

**Exclusions**:
- Does not perform full AST parsing (using Roslyn/TypeScript Compiler API is future work)
- Does not resolve symbol references (finding where a method is called)
- Does not track symbol renames across files

#### 3.1.3 Import/Reference Index
**Feature**: Track which files import/reference other files (for context relevance scoring).

**Capabilities**:
- Detect import statements using regex:
  - C#: `using Namespace;`, `using Alias = Namespace;`
  - TypeScript: `import ... from '...'`, `require('...')`
  - Python: `import ...`, `from ... import ...`
- Store mapping: `FilePath → List<ImportedFilePath>`
- Resolve relative imports to absolute paths (e.g., `'./utils'` → `src/utils.ts`)
- Track external vs. internal imports (external = npm packages, NuGet packages; internal = workspace files)

**Input/Output**:
- Input: File path, file content, language ID
- Output: `ImportReference[]` array

**Boundary Conditions**:
- Maximum imports per file: 500
- Unresolved imports (file not found): logged as warning, stored as unresolved reference
- Circular imports: detected but not treated as error (common in JavaScript)

**Dependencies**:
- File index (to resolve import paths)

**Exclusions**:
- Does not analyze runtime dynamic imports (`import()` function)
- Does not track using directives at the file level for C# (only namespace-level)

#### 3.1.4 Fuzzy Search
**Feature**: Fast fuzzy matching for files and symbols.

**Capabilities**:
- **File search**: Match against file path (relative to workspace root)
- **Symbol search**: Match against symbol name or fully-qualified name (e.g., `MyClass.MyMethod`)
- Scoring algorithm (descending priority):
  1. **Exact match** (score = 1000): query exactly equals candidate
  2. **Prefix match** (score = 500 + match length): query is prefix of candidate
  3. **Word boundary match** (score = 300): query matches start of word in camelCase/PascalCase/snake_case
  4. **Contains match** (score = 100): query is substring of candidate
  5. **Subsequence match** (score = 50): all query characters appear in order in candidate
- Case-insensitive matching
- Return top N results (default N=50) sorted by score descending

**Input/Output**:
- Input: Query string, search mode (`File` or `Symbol`), max results
- Output: `SearchResult[]` with path/name, score, context (file path for symbols)

**Boundary Conditions**:
- Empty query: return no results
- Query length > 100 characters: truncate to 100
- Special characters in query: escaped/sanitized
- Timeout: search must complete within 10ms for 10K files (enforced with `CancellationTokenSource`)

**Dependencies**:
- File index or symbol index (depending on search mode)

**Exclusions**:
- Does not use ML-based ranking (future enhancement)
- Does not support regex queries (just literal fuzzy matching)

#### 3.1.5 @ Mention Completion
**Feature**: Provide autocomplete when user types `@` in chat input.

**Capabilities**:
- Trigger on `@` character
- Display file picker by default (show all workspace files ranked by recent usage)
- Switch to symbol picker on `@#` (show all symbols)
- Filter results as user types (e.g., `@MyFile` filters to files matching "MyFile")
- Display format:
  - File mention: `@filename.cs` (shows relative path in UI)
  - Symbol mention: `@#ClassName.MethodName` (shows file path + line in UI)
- On selection: insert mention tag into chat, load content/symbol definition into AI context

**Input/Output**:
- Input: Current chat input text, cursor position
- Output: Completion items with label, kind (file/symbol), insert text, detail (file path)

**Boundary Conditions**:
- Maximum completion items shown: 50
- Minimum query length to trigger: 1 character after `@`
- Debouncing: 100ms delay before running search (to avoid lag while typing)

**Dependencies**:
- Fuzzy search (for filtering)
- File/symbol index

**Exclusions**:
- Does not support `@folder/` to mention entire directories (future work)
- Does not support `@line:123` to mention specific line ranges

#### 3.1.6 Context Relevance Scoring
**Feature**: Rank files by relevance when building AI context (to fit within token budget).

**Capabilities**:
- Compute relevance score for each file based on:
  - **Currently open file** (score = 100): highest priority
  - **Recently edited files** (score = 50, decay over time): files modified in last 5 minutes
  - **Import relationships** (score = 30): files imported by currently open file
  - **Imported by relationships** (score = 20): files that import currently open file
  - **Same directory** (score = 10): files in same folder as open file
- Sort files by score descending
- Enforce token budget: estimate tokens per file (rough heuristic: 1 token ≈ 4 characters), stop adding files when budget exceeded

**Input/Output**:
- Input: Currently open file, recent edits list, token budget (e.g., 8000 tokens)
- Output: `ContextFile[]` array sorted by relevance, truncated to fit budget

**Boundary Conditions**:
- If currently open file + its imports exceed budget: include only currently open file
- If no file is open: use most recently edited file as anchor
- Token budget range: 1000 (minimum) to 100,000 (maximum)

**Dependencies**:
- File index, import index
- Editor state (which file is currently open)

**Exclusions**:
- Does not use AI embeddings for semantic similarity (future enhancement)
- Does not learn user preferences over time (future ML feature)

---

### 3.2 Interaction Dimension (交互维度)

#### 3.2.1 Initial Indexing Flow
**User Operation Path**:
1. User opens workspace folder or .mcw file
2. `WorkspaceService` fires `WorkspaceFoldersChanged` event
3. `IndexingService` subscribes to event, starts background indexing task
4. UI shows progress notification: "Indexing workspace... (123/1000 files)"
5. User can continue working (indexing is non-blocking)
6. On completion: notification updates to "Indexing complete (1000 files, 5432 symbols)"
7. Search and @ mention features become fully available

**State Transitions**:
- `NotStarted` → `Scanning` (on workspace open)
- `Scanning` → `Indexing` (file list enumerated, parsing symbols)
- `Indexing` → `Ready` (all files processed)
- `Ready` → `Updating` (incremental update triggered by file change)
- `Updating` → `Ready` (update complete)
- Any state → `Cancelled` (user closes workspace)

**Exception Flows**:
- **File access denied**: Log warning, skip file, continue indexing
- **Malformed code**: Log warning, store partial symbols, continue
- **Out of memory**: Trigger GC, retry once, if fails: cancel indexing and show error
- **Workspace closed during indexing**: Cancel background task via `CancellationToken`, dispose resources

**Feedback Mechanisms**:
- Progress bar in status bar (bottom of window)
- Notification toast on completion (dismissible)
- Console log output for debugging (visible in dev tools)

**Accessibility**:
- Screen reader announces "Indexing started" and "Indexing complete"
- Progress percentage readable via ARIA attributes

#### 3.2.2 Incremental Update Flow
**User Operation Path**:
1. User edits `MyFile.cs` and saves
2. `FileWatcherService` fires `FilesChanged` event (debounced 300ms)
3. `IndexingService` receives event, computes diff (hash comparison)
4. If content changed: re-parse symbols for that file only
5. Update file index entry (new timestamp, hash)
6. Update symbol index (remove old symbols, add new ones)
7. Update import index if import statements changed
8. No UI notification (silent update)

**State Transitions**:
- `Ready` → `Updating` → `Ready` (fast, typically < 100ms)

**Exception Flows**:
- **File deleted**: Remove from all indexes, clean up orphaned symbols
- **File renamed**: Treat as delete + create (via `FileWatcherService.FileRenamed`)
- **Multiple rapid changes**: Debouncing ensures single update after changes settle

**Feedback Mechanisms**:
- No explicit UI feedback (invisible to user for speed)
- Debug log: "Updated index for MyFile.cs (15 symbols)"

#### 3.2.3 File Search Interaction (Ctrl+P equivalent)
**User Operation Path**:
1. User presses `Ctrl+P` (or clicks "Go to File" command)
2. Modal input box appears with focus
3. User types query: "myfile"
4. On each keystroke (debounced 50ms): fuzzy search executes
5. Results update in real-time below input
6. User navigates results with arrow keys, presses Enter to select
7. Selected file opens in editor
8. Modal closes

**State Transitions**:
- `Closed` → `Open` (on Ctrl+P)
- `Open` → `Searching` (on keystroke)
- `Searching` → `ShowingResults` (search completes)
- `ShowingResults` → `Closed` (on Enter/Escape)

**Exception Flows**:
- **No results**: Show "No files found" message
- **Search timeout** (> 10ms): Cancel, show partial results, log warning
- **User closes modal during search**: Cancel search via token

**Feedback Mechanisms**:
- Results count shown: "12 files"
- Matched characters highlighted in results
- Keyboard shortcuts shown in modal footer

#### 3.2.4 @ Mention Interaction
**User Operation Path**:
1. User types `@` in chat input
2. Autocomplete dropdown appears below cursor
3. Shows top 50 files by recent usage
4. User types more: `@myf`
5. Dropdown filters to files matching "myf"
6. User presses Down arrow to navigate, Enter to select
7. Selected file inserts as `@MyFile.cs`
8. Dropdown closes
9. On sending message: file content loaded into AI context

**Alternative Flow (Symbol Mention)**:
1. User types `@#`
2. Autocomplete shows symbols instead of files
3. User types: `@#MyClass.MyMethod`
4. Selects symbol
5. Inserts as `@#MyClass.MyMethod`
6. On send: symbol definition (surrounding 20 lines) loaded into context

**State Transitions**:
- `Closed` → `FileMode` (on `@`)
- `FileMode` → `SymbolMode` (on `#`)
- `SymbolMode` → `FileMode` (on backspace deleting `#`)
- Any mode → `Closed` (on Escape or selection)

**Exception Flows**:
- **No matches**: Show "No files/symbols found"
- **File deleted after mention inserted**: Show warning icon next to mention, exclude from context
- **Very long file** (> 10MB): Show warning "File too large for context", include only first 10K lines

**Feedback Mechanisms**:
- Mention rendered as chip/pill UI element in chat input
- Hover over mention shows file path + size
- Close button (X) on mention to remove

---

### 3.3 Performance Dimension (性能维度)

#### 3.3.1 Initial Indexing Performance
**Metrics**:
- **1,000 files**: < 3 seconds (target: 2s, maximum: 3s)
- **10,000 files**: < 10 seconds (target: 8s, maximum: 10s)
- **100,000 files**: < 90 seconds (target: 60s, maximum: 90s)

**Load Scenarios**:
- Normal: Typical workspace with 1K-5K files (average C# solution)
- Peak: Large monorepo with 50K-100K files (enterprise codebase)
- Stress test: 100K files across 10 workspace folders

**Resource Constraints**:
- CPU: Use all available cores (parallel file parsing with `Parallel.ForEach`)
- Memory: < 50MB for 10K file index (≈5KB per file average)
- Disk I/O: Batch file reads (1000 files per batch) to reduce seek time

**Degradation Strategy**:
- If indexing exceeds 10s: Show "Large workspace detected, indexing may take a while" warning
- If memory usage exceeds 500MB: Switch to streaming mode (index in chunks, flush to disk)
- If indexing exceeds 60s: Offer "Cancel indexing" button

#### 3.3.2 Incremental Update Performance
**Metrics**:
- **Single file update**: < 100ms (target: 50ms, maximum: 100ms)
- **Batch update (10 files)**: < 500ms (target: 300ms, maximum: 500ms)

**Resource Constraints**:
- Must not block UI thread (run on background thread)
- Lock acquisition time: < 5ms (use `ReaderWriterLockSlim` with timeout)

**Degradation Strategy**:
- If update queue exceeds 100 pending files: Trigger full re-index (faster than processing huge queue)

#### 3.3.3 Search Performance
**Metrics**:
- **File search (10K files)**: < 10ms (target: 5ms, maximum: 10ms)
- **Symbol search (50K symbols)**: < 20ms (target: 10ms, maximum: 20ms)
- **@ mention filtering**: < 5ms per keystroke (target: 2ms, maximum: 5ms)

**Load Scenarios**:
- Normal: Query length 3-10 characters, 50 results
- Peak: Query length 1 character (matches everything), 50 results
- Stress test: Query on 100K files, 1M symbols

**Resource Constraints**:
- Search must run on background thread to avoid UI jank
- Use `CancellationToken` to abort slow searches

**Degradation Strategy**:
- If search exceeds 10ms: Return partial results, log warning
- If query matches > 10K items: Limit to top 50 by score, show "Too many results" message

#### 3.3.4 Memory Usage
**Metrics**:
- **File index**: ≈5KB per file (path + metadata)
  - 10K files = 50MB
  - 100K files = 500MB
- **Symbol index**: ≈200 bytes per symbol (name + signature + position)
  - 50K symbols (5 per file for 10K files) = 10MB
  - 500K symbols (5 per file for 100K files) = 100MB
- **Import index**: ≈100 bytes per import reference
  - 20K imports (2 per file for 10K files) = 2MB

**Total Memory Budget**:
- 10K files: **62MB** (50MB files + 10MB symbols + 2MB imports)
- Target: Stay under 50MB for 10K files → requires optimization

**Optimization Strategies**:
- Use `string.Intern()` for repeated strings (file paths, language IDs)
- Store file paths relative to workspace root (shorter strings)
- Use `Memory<char>` instead of `string` for large signatures
- Compress infrequently accessed data (e.g., full signatures stored as gzip'd bytes)

---

### 3.4 Quality Dimension (质量维度)

#### 3.4.1 Test Coverage Requirements
**Unit Tests**:
- Language detection: 100% coverage (all extensions mapped correctly)
- Fuzzy matching algorithm: 100% coverage (all scoring paths tested)
- Symbol extraction regex: 90% coverage (all major symbol types, edge cases)
- Index update logic: 95% coverage (add/remove/update operations)

**Integration Tests**:
- Full indexing workflow: End-to-end test with real workspace (10 files)
- Incremental updates: Simulate file edit, verify index updated correctly
- Search performance: Benchmark with 10K file dataset
- @ mention insertion: UI automation test (requires Avalonia test framework)

**Critical Paths Requiring Integration Tests**:
1. Workspace open → Index build → Search works
2. File edited → Index updated → Search finds new symbol
3. File deleted → Index cleaned → Search doesn't return deleted file
4. @ mention → File content loaded → AI receives correct context

#### 3.4.2 Error Handling Specifications
**Category: File I/O Errors**
- **File not found** (deleted during indexing): Skip, log warning, continue
- **Access denied**: Skip, log warning (include file path), continue
- **Disk full** (if persisting index): Show error dialog, fall back to in-memory only

**Category: Parsing Errors**
- **Malformed regex** (should never happen): Log error, use fallback empty pattern, continue
- **Regex timeout** (catastrophic backtracking): Abort after 100ms, log warning, return partial symbols
- **Invalid UTF-8** (binary file mistaken for text): Detect BOM, skip if binary, log warning

**Category: Concurrency Errors**
- **Lock timeout** (couldn't acquire read/write lock in 5s): Log error, throw exception, retry operation
- **Collection modified during enumeration**: Wrap in try-catch, retry with fresh snapshot
- **Task cancelled** (workspace closed): Dispose resources cleanly, no error logged (expected flow)

**Category: Resource Exhaustion**
- **Out of memory**: Trigger GC, retry once, if fails: show error "Workspace too large to index fully"
- **Stack overflow** (deeply nested symbols): Limit recursion depth to 50, log warning
- **Too many files** (> 100K): Show warning, continue indexing, may be slow

#### 3.4.3 Reliability Targets
**Uptime**: Not applicable (indexing service lifetime = workspace lifetime)

**Mean Time Between Failures (MTBF)**:
- Indexing should not crash: 99.9% success rate (< 1 crash per 1000 workspace opens)

**Recovery Time Objective (RTO)**:
- If indexing crashes: Restart automatically on next file change event
- If index corrupted: Rebuild from scratch (< 10s for typical workspace)

**Data Integrity**:
- Index must stay consistent with file system: Verification test on every file change
- If inconsistency detected: Log error, trigger incremental re-index for affected files

#### 3.4.4 Data Integrity Guarantees
**Consistency**:
- File index MUST reflect actual file system state after indexing completes
- Symbol index MUST match file content (verified via content hash)
- Import index MUST be transactionally updated with symbol index (both or neither)

**Durability**:
- In-memory index (MVP): Lost on restart, acceptable for MVP
- Future (persisted index): Write to SQLite with WAL mode, ACID guarantees

**Backup Requirements**:
- Not applicable for in-memory index
- Future: Export index snapshot to `.mcc-index/` folder for debugging

---

### 3.5 Extensibility Dimension (扩展维度)

#### 3.5.1 Abstraction Layers
**Interfaces**:
```csharp
public interface ILanguageDetector
{
    string? DetectLanguage(string filePath);
    bool ShouldIndex(string filePath);
}

public interface ISymbolExtractor
{
    SymbolIndexEntry[] ExtractSymbols(string filePath, string content, string languageId);
}

public interface IImportResolver
{
    ImportReference[] ResolveImports(string filePath, string content, string languageId);
}

public interface IFuzzyMatcher
{
    SearchResult[] Search(string query, SearchMode mode, int maxResults);
}

public interface IIndexStorage
{
    void AddFile(FileIndexEntry entry);
    void RemoveFile(string filePath);
    void AddSymbols(string filePath, SymbolIndexEntry[] symbols);
    void Clear();
}
```

**Plugin Points**:
- Register custom `ISymbolExtractor` for new languages (e.g., Rust, Go)
- Register custom `IFuzzyMatcher` for advanced ranking (e.g., ML-based)
- Swap `IIndexStorage` implementation (in-memory → SQLite → remote index server)

**Dependency Injection**:
- Use constructor injection for all services
- Register in `ServiceCollection`:
  ```csharp
  services.AddSingleton<ILanguageDetector, LanguageDetector>();
  services.AddSingleton<ISymbolExtractor, RegexSymbolExtractor>();
  services.AddSingleton<IIndexStorage, InMemoryIndexStorage>();
  services.AddSingleton<IndexingService>();
  ```

#### 3.5.2 Reserved Capabilities
**Hot-Reload Support**:
- Language detector patterns configurable via JSON file (no code recompile needed)
- Symbol extraction regex patterns hot-reloadable from `symbol-patterns.json`
- Ignore patterns configurable in workspace settings

**Dynamic Loading**:
- Load language extractors from external DLLs (future plugin system)
- Support index format versioning (v1, v2, ...) for backward compatibility

**Feature Flags**:
- `EnableSymbolIndexing`: Disable symbol extraction, only index files (faster)
- `EnableImportAnalysis`: Disable import tracking (simpler index)
- `EnablePersistence`: Switch between in-memory and disk-based index
- `UseParallelIndexing`: Toggle parallel vs. sequential indexing (for debugging)

#### 3.5.3 Migration Strategy
**Version 1 → Version 2 (add full-text search)**:
- Keep existing file/symbol index unchanged
- Add new `ContentIndex` with inverted index for text search
- No breaking changes to existing APIs

**Version 2 → Version 3 (add Roslyn-based parsing)**:
- Replace `RegexSymbolExtractor` with `RoslynSymbolExtractor` for C#
- Implement same `ISymbolExtractor` interface → transparent swap
- Existing index schema unchanged (just better symbol accuracy)

**Backward Compatibility**:
- Index format includes version header: `{ "version": 1, "data": {...} }`
- On version mismatch: Rebuild index (fast operation)

#### 3.5.4 Configuration Management
**Configurable Settings** (via workspace settings or global config):
```json
{
  "indexing.enabled": true,
  "indexing.maxFileSize": 10485760, // 10MB in bytes
  "indexing.excludePatterns": [".git/**", "node_modules/**"],
  "indexing.languageExtensions": {
    ".cs": "csharp",
    ".ts": "typescript"
  },
  "indexing.symbolPatterns": {
    "csharp": {
      "class": "\\bclass\\s+(\\w+)",
      "method": "\\b(public|private|protected)?\\s+\\w+\\s+(\\w+)\\s*\\("
    }
  },
  "indexing.performance.parallelism": 0, // 0 = auto (CPU count)
  "indexing.performance.batchSize": 1000
}
```

**Hardcoded Constants** (not configurable):
- Lock timeout: 5000ms (changing this is dangerous)
- Debounce delay: 300ms (matches `FileWatcherService`)
- Search timeout: 10ms (performance contract)

---

## 4. Index Schema Design

### 4.1 File Index Entry
```csharp
public class FileIndexEntry
{
    public string FilePath { get; set; }           // Relative to workspace root
    public string AbsolutePath { get; set; }       // Full path
    public string LanguageId { get; set; }         // "csharp", "typescript", etc.
    public long FileSize { get; set; }             // Bytes
    public DateTime LastModified { get; set; }     // UTC timestamp
    public string ContentHash { get; set; }        // SHA256 of first 4KB
    public int WorkspaceFolderIndex { get; set; }  // Which workspace folder (for multi-root)
}
```

**Storage**: `Dictionary<string, FileIndexEntry>` keyed by `FilePath` (case-insensitive)

**Indexing**: Secondary index `Dictionary<string, List<FileIndexEntry>>` keyed by `LanguageId` for fast language-specific queries

### 4.2 Symbol Index Entry
```csharp
public enum SymbolKind
{
    Class,
    Interface,
    Method,
    Property,
    Function,
    Enum,
    Struct,
    Field,
    Const,
    Type
}

public class SymbolIndexEntry
{
    public string Name { get; set; }               // "MyMethod"
    public SymbolKind Kind { get; set; }           // Method
    public string FilePath { get; set; }           // Relative path
    public int Line { get; set; }                  // 1-indexed
    public int Column { get; set; }                // 0-indexed
    public string Signature { get; set; }          // "public void MyMethod(int x)"
    public string? ParentSymbol { get; set; }      // "MyClass" (null for top-level)
    public string? Visibility { get; set; }        // "public", "private", "protected", null
}
```

**Storage**: `Dictionary<string, List<SymbolIndexEntry>>` keyed by `FilePath`

**Indexing**: Secondary index `Dictionary<string, List<SymbolIndexEntry>>` keyed by `Name` for fast symbol search

### 4.3 Import Reference Entry
```csharp
public enum ImportKind
{
    Internal,  // Workspace file
    External   // Package/library
}

public class ImportReference
{
    public string ImporterFilePath { get; set; }   // File doing the import
    public string ImportedPath { get; set; }       // Imported namespace/module/file
    public ImportKind Kind { get; set; }           // Internal or External
    public string? ResolvedFilePath { get; set; }  // Absolute path (if internal)
}
```

**Storage**: `Dictionary<string, List<ImportReference>>` keyed by `ImporterFilePath`

**Indexing**: Reverse index `Dictionary<string, List<string>>` keyed by `ResolvedFilePath` (for "imported by" queries)

---

## 5. Language Detection

### 5.1 Extension Mapping
```csharp
private static readonly Dictionary<string, string> ExtensionToLanguage = new()
{
    // C# / .NET
    [".cs"] = "csharp",
    [".csx"] = "csharp",
    [".cake"] = "csharp",

    // TypeScript / JavaScript
    [".ts"] = "typescript",
    [".tsx"] = "typescriptreact",
    [".js"] = "javascript",
    [".jsx"] = "javascriptreact",
    [".mjs"] = "javascript",
    [".cjs"] = "javascript",

    // Python
    [".py"] = "python",
    [".pyi"] = "python",
    [".pyw"] = "python",

    // Rust
    [".rs"] = "rust",

    // Go
    [".go"] = "go",

    // Java
    [".java"] = "java",

    // C / C++
    [".c"] = "c",
    [".h"] = "c",
    [".cpp"] = "cpp",
    [".cc"] = "cpp",
    [".cxx"] = "cpp",
    [".hpp"] = "cpp",

    // Web
    [".html"] = "html",
    [".htm"] = "html",
    [".css"] = "css",
    [".scss"] = "scss",
    [".sass"] = "sass",
    [".less"] = "less",

    // Markup
    [".md"] = "markdown",
    [".json"] = "json",
    [".xml"] = "xml",
    [".yaml"] = "yaml",
    [".yml"] = "yaml",
    [".toml"] = "toml",

    // Shell
    [".sh"] = "shellscript",
    [".bash"] = "shellscript",
    [".zsh"] = "shellscript",
    [".ps1"] = "powershell",
};
```

### 5.2 Ignore Patterns
**Directory Patterns** (entire directory ignored recursively):
- `.git/`
- `node_modules/`
- `bin/`
- `obj/`
- `.vs/`
- `.idea/`
- `__pycache__/`
- `.venv/`
- `venv/`
- `.next/`
- `dist/`
- `build/`
- `.cursor/`
- `coverage/`
- `.nyc_output/`
- `target/` (Rust)
- `packages/` (NuGet)

**File Patterns**:
- `*.dll`
- `*.exe`
- `*.so`
- `*.dylib`
- `*.a`
- `*.o`
- `*.obj`
- `*.pdb`
- `*.png`, `*.jpg`, `*.jpeg`, `*.gif`, `*.bmp`, `*.ico`, `*.svg`
- `*.mp4`, `*.avi`, `*.mov`
- `*.zip`, `*.tar`, `*.gz`, `*.7z`
- `*.min.js` (minified files - too large, no value)
- `*.bundle.js`

**Detection Logic**:
```csharp
public bool ShouldIndex(string filePath)
{
    var segments = filePath.Split(Path.DirectorySeparatorChar);

    // Check directory patterns
    if (segments.Any(s => IgnoredDirectories.Contains(s)))
        return false;

    var ext = Path.GetExtension(filePath);

    // Check file patterns
    if (IgnoredExtensions.Contains(ext))
        return false;

    // Check if language is supported
    if (!ExtensionToLanguage.ContainsKey(ext))
        return false; // Unknown file type

    // Check file size
    var fileInfo = new FileInfo(filePath);
    if (fileInfo.Length > MaxFileSize)
        return false; // Too large

    return true;
}
```

---

## 6. Symbol Extraction (Regex-based MVP)

### 6.1 C# Symbol Patterns
```csharp
private static readonly SymbolPattern[] CSharpPatterns = new[]
{
    // Class
    new SymbolPattern
    {
        Kind = SymbolKind.Class,
        Regex = @"^\s*(public|internal|private|protected)?\s*(static|abstract|sealed|partial)?\s*class\s+(\w+)",
        NameGroupIndex = 3
    },

    // Interface
    new SymbolPattern
    {
        Kind = SymbolKind.Interface,
        Regex = @"^\s*(public|internal|private|protected)?\s*interface\s+(\w+)",
        NameGroupIndex = 2
    },

    // Struct
    new SymbolPattern
    {
        Kind = SymbolKind.Struct,
        Regex = @"^\s*(public|internal|private|protected)?\s*(readonly)?\s*struct\s+(\w+)",
        NameGroupIndex = 3
    },

    // Enum
    new SymbolPattern
    {
        Kind = SymbolKind.Enum,
        Regex = @"^\s*(public|internal|private|protected)?\s*enum\s+(\w+)",
        NameGroupIndex = 2
    },

    // Record
    new SymbolPattern
    {
        Kind = SymbolKind.Class, // Treat as class
        Regex = @"^\s*(public|internal|private|protected)?\s*record\s+(class|struct)?\s*(\w+)",
        NameGroupIndex = 3
    },

    // Method
    new SymbolPattern
    {
        Kind = SymbolKind.Method,
        Regex = @"^\s*(public|internal|private|protected)?\s*(static|virtual|override|abstract|async)?\s+[\w<>[\],\s]+\s+(\w+)\s*\(",
        NameGroupIndex = 3
    },

    // Property (auto-property or with getter)
    new SymbolPattern
    {
        Kind = SymbolKind.Property,
        Regex = @"^\s*(public|internal|private|protected)?\s*(static|virtual|override|abstract)?\s+[\w<>[\],\s]+\s+(\w+)\s*\{\s*(get|set)",
        NameGroupIndex = 3
    },

    // Field
    new SymbolPattern
    {
        Kind = SymbolKind.Field,
        Regex = @"^\s*(public|internal|private|protected)?\s*(static|readonly|const)?\s+[\w<>[\],\s]+\s+(\w+)\s*[=;]",
        NameGroupIndex = 3
    }
};
```

### 6.2 TypeScript Symbol Patterns
```csharp
private static readonly SymbolPattern[] TypeScriptPatterns = new[]
{
    // Class
    new SymbolPattern
    {
        Kind = SymbolKind.Class,
        Regex = @"^\s*(export\s+)?(abstract\s+)?class\s+(\w+)",
        NameGroupIndex = 3
    },

    // Interface
    new SymbolPattern
    {
        Kind = SymbolKind.Interface,
        Regex = @"^\s*(export\s+)?interface\s+(\w+)",
        NameGroupIndex = 2
    },

    // Type alias
    new SymbolPattern
    {
        Kind = SymbolKind.Type,
        Regex = @"^\s*(export\s+)?type\s+(\w+)\s*=",
        NameGroupIndex = 2
    },

    // Enum
    new SymbolPattern
    {
        Kind = SymbolKind.Enum,
        Regex = @"^\s*(export\s+)?(const\s+)?enum\s+(\w+)",
        NameGroupIndex = 3
    },

    // Function (standalone)
    new SymbolPattern
    {
        Kind = SymbolKind.Function,
        Regex = @"^\s*(export\s+)?(async\s+)?function\s+(\w+)\s*\(",
        NameGroupIndex = 3
    },

    // Const function (arrow or regular)
    new SymbolPattern
    {
        Kind = SymbolKind.Const,
        Regex = @"^\s*(export\s+)?const\s+(\w+)\s*[:=]\s*(async\s*)?\(",
        NameGroupIndex = 2
    },

    // Method (inside class)
    new SymbolPattern
    {
        Kind = SymbolKind.Method,
        Regex = @"^\s*(public|private|protected)?\s*(static|async)?\s*(\w+)\s*\(",
        NameGroupIndex = 3
    }
};
```

### 6.3 Python Symbol Patterns
```csharp
private static readonly SymbolPattern[] PythonPatterns = new[]
{
    // Class
    new SymbolPattern
    {
        Kind = SymbolKind.Class,
        Regex = @"^\s*class\s+(\w+)",
        NameGroupIndex = 1
    },

    // Function (async or regular)
    new SymbolPattern
    {
        Kind = SymbolKind.Function,
        Regex = @"^\s*(async\s+)?def\s+(\w+)\s*\(",
        NameGroupIndex = 2
    }
};
```

### 6.4 Extraction Algorithm
```csharp
public SymbolIndexEntry[] ExtractSymbols(string filePath, string content, string languageId)
{
    var patterns = GetPatternsForLanguage(languageId);
    if (patterns == null) return Array.Empty<SymbolIndexEntry>();

    var symbols = new List<SymbolIndexEntry>();
    var lines = content.Split('\n');
    string? currentParent = null;
    int currentIndent = 0;

    for (int i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        var trimmedLine = line.TrimStart();
        var indent = line.Length - trimmedLine.Length;

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(line, pattern.Regex, RegexOptions.None, TimeSpan.FromMilliseconds(100));
            if (!match.Success) continue;

            var name = match.Groups[pattern.NameGroupIndex].Value;
            var visibility = match.Groups[1].Success ? match.Groups[1].Value : null;

            // Track parent for nested symbols
            if (pattern.Kind == SymbolKind.Class || pattern.Kind == SymbolKind.Interface)
            {
                currentParent = name;
                currentIndent = indent;
            }
            else if (indent <= currentIndent)
            {
                currentParent = null; // Exited nested scope
            }

            symbols.Add(new SymbolIndexEntry
            {
                Name = name,
                Kind = pattern.Kind,
                FilePath = filePath,
                Line = i + 1, // 1-indexed
                Column = line.IndexOf(name),
                Signature = trimmedLine.TrimEnd(),
                ParentSymbol = currentParent,
                Visibility = visibility
            });

            break; // Only one pattern per line
        }
    }

    return symbols.ToArray();
}
```

---

## 7. Fuzzy Search Algorithm

### 7.1 Scoring Function
```csharp
public int CalculateScore(string query, string candidate)
{
    query = query.ToLowerInvariant();
    candidate = candidate.ToLowerInvariant();

    // 1. Exact match
    if (query == candidate)
        return 1000;

    // 2. Prefix match
    if (candidate.StartsWith(query))
        return 500 + query.Length;

    // 3. Word boundary match (camelCase, PascalCase, snake_case)
    var wordStarts = GetWordBoundaryIndices(candidate);
    foreach (var idx in wordStarts)
    {
        if (candidate.Substring(idx).StartsWith(query))
            return 300;
    }

    // 4. Contains match
    var containsIndex = candidate.IndexOf(query);
    if (containsIndex >= 0)
        return 100;

    // 5. Subsequence match
    if (IsSubsequence(query, candidate))
        return 50;

    return 0; // No match
}

private List<int> GetWordBoundaryIndices(string s)
{
    var indices = new List<int> { 0 }; // Always include start

    for (int i = 1; i < s.Length; i++)
    {
        // Uppercase after lowercase (camelCase boundary)
        if (char.IsUpper(s[i]) && char.IsLower(s[i - 1]))
            indices.Add(i);

        // After underscore or dash (snake_case, kebab-case)
        if ((s[i - 1] == '_' || s[i - 1] == '-') && char.IsLetterOrDigit(s[i]))
            indices.Add(i);
    }

    return indices;
}

private bool IsSubsequence(string query, string candidate)
{
    int j = 0;
    for (int i = 0; i < candidate.Length && j < query.Length; i++)
    {
        if (candidate[i] == query[j])
            j++;
    }
    return j == query.Length;
}
```

### 7.2 Search Implementation
```csharp
public SearchResult[] Search(string query, SearchMode mode, int maxResults)
{
    if (string.IsNullOrWhiteSpace(query))
        return Array.Empty<SearchResult>();

    var candidates = mode == SearchMode.File
        ? _fileIndex.Values.Select(f => new { Item = (object)f, Name = f.FilePath })
        : _symbolIndex.Values.SelectMany(s => s).Select(s => new { Item = (object)s, Name = s.Name });

    var scored = candidates
        .Select(c => new
        {
            c.Item,
            Score = CalculateScore(query, c.Name)
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .Take(maxResults)
        .ToArray();

    return scored.Select(x => new SearchResult
    {
        Item = x.Item,
        Score = x.Score
    }).ToArray();
}
```

### 7.3 Performance Optimization
- **Parallel search**: Use `AsParallel()` for > 10K candidates
- **Early termination**: Stop after finding 1000+ matches (before sorting)
- **Caching**: Cache last 10 query results (LRU cache)

---

## 8. @ Mention Completion

### 8.1 Trigger Detection
```csharp
public bool ShouldShowCompletion(string text, int cursorPosition)
{
    if (cursorPosition == 0) return false;

    var charBefore = text[cursorPosition - 1];

    // Trigger on '@' if not inside word
    if (charBefore == '@')
    {
        if (cursorPosition == 1) return true;
        var charBefore2 = text[cursorPosition - 2];
        return char.IsWhiteSpace(charBefore2) || charBefore2 == '\n';
    }

    // Continue showing if already triggered
    if (cursorPosition >= 2 && text[cursorPosition - 2] == '@')
        return true;

    return false;
}

public CompletionMode GetCompletionMode(string text, int cursorPosition)
{
    // Find the '@' that triggered completion
    for (int i = cursorPosition - 1; i >= 0; i--)
    {
        if (text[i] == '@')
        {
            var afterAt = text.Substring(i + 1, cursorPosition - i - 1);
            return afterAt.StartsWith('#') ? CompletionMode.Symbol : CompletionMode.File;
        }
        if (char.IsWhiteSpace(text[i])) break;
    }

    return CompletionMode.None;
}
```

### 8.2 Completion Item Generation
```csharp
public CompletionItem[] GetCompletions(string query, CompletionMode mode)
{
    if (mode == CompletionMode.File)
    {
        var results = _fuzzyMatcher.Search(query, SearchMode.File, 50);
        return results.Select(r => new CompletionItem
        {
            Label = Path.GetFileName(((FileIndexEntry)r.Item).FilePath),
            Kind = CompletionItemKind.File,
            Detail = ((FileIndexEntry)r.Item).FilePath,
            InsertText = $"@{Path.GetFileName(((FileIndexEntry)r.Item).FilePath)}",
            Data = r.Item
        }).ToArray();
    }
    else // Symbol mode
    {
        var symbolQuery = query.TrimStart('#');
        var results = _fuzzyMatcher.Search(symbolQuery, SearchMode.Symbol, 50);
        return results.Select(r =>
        {
            var symbol = (SymbolIndexEntry)r.Item;
            var fullName = symbol.ParentSymbol != null
                ? $"{symbol.ParentSymbol}.{symbol.Name}"
                : symbol.Name;

            return new CompletionItem
            {
                Label = fullName,
                Kind = ToCompletionItemKind(symbol.Kind),
                Detail = $"{symbol.FilePath}:{symbol.Line}",
                InsertText = $"@#{fullName}",
                Data = r.Item
            };
        }).ToArray();
    }
}
```

### 8.3 Context Injection
```csharp
public string BuildAIContext(List<MentionTag> mentions, int tokenBudget)
{
    var sb = new StringBuilder();
    int usedTokens = 0;

    foreach (var mention in mentions)
    {
        string content;

        if (mention.Type == MentionType.File)
        {
            var file = _fileIndex[mention.Path];
            content = File.ReadAllText(file.AbsolutePath);

            // Truncate if too large
            if (content.Length > 100000) // ~25K tokens
                content = content.Substring(0, 100000) + "\n... (truncated)";
        }
        else // Symbol
        {
            var symbol = mention.Symbol;
            var lines = File.ReadAllLines(symbol.FilePath);

            // Extract surrounding context (20 lines before/after)
            int startLine = Math.Max(0, symbol.Line - 20);
            int endLine = Math.Min(lines.Length, symbol.Line + 20);

            content = string.Join('\n', lines[startLine..endLine]);
        }

        var tokens = EstimateTokens(content);
        if (usedTokens + tokens > tokenBudget)
        {
            sb.AppendLine($"... (remaining mentions excluded due to token budget)");
            break;
        }

        sb.AppendLine($"--- {mention.DisplayName} ---");
        sb.AppendLine(content);
        sb.AppendLine();

        usedTokens += tokens;
    }

    return sb.ToString();
}

private int EstimateTokens(string text)
{
    // Rough heuristic: 1 token ≈ 4 characters
    return text.Length / 4;
}
```

---

## 9. Context Relevance Scoring

### 9.1 Scoring Algorithm
```csharp
public class ContextRelevanceScorer
{
    public List<ContextFile> RankFiles(
        string? currentFilePath,
        List<string> recentEditedFiles,
        int tokenBudget)
    {
        var scores = new Dictionary<string, int>();

        // Score all files
        foreach (var file in _fileIndex.Values)
        {
            int score = 0;

            // Currently open file
            if (file.FilePath == currentFilePath)
                score += 100;

            // Recently edited (decay over time)
            var recentIndex = recentEditedFiles.IndexOf(file.FilePath);
            if (recentIndex >= 0)
                score += 50 - (recentIndex * 5); // Decay: 50, 45, 40, ...

            // Imported by current file
            if (currentFilePath != null && _importIndex.TryGetValue(currentFilePath, out var imports))
            {
                if (imports.Any(i => i.ResolvedFilePath == file.FilePath))
                    score += 30;
            }

            // Imports current file
            if (currentFilePath != null && _reverseImportIndex.TryGetValue(currentFilePath, out var importers))
            {
                if (importers.Contains(file.FilePath))
                    score += 20;
            }

            // Same directory as current file
            if (currentFilePath != null)
            {
                var currentDir = Path.GetDirectoryName(currentFilePath);
                var fileDir = Path.GetDirectoryName(file.FilePath);
                if (currentDir == fileDir)
                    score += 10;
            }

            scores[file.FilePath] = score;
        }

        // Sort by score descending
        var ranked = scores
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => new ContextFile
            {
                FilePath = kvp.Key,
                RelevanceScore = kvp.Value,
                EstimatedTokens = EstimateFileTokens(kvp.Key)
            })
            .ToList();

        // Apply token budget
        var result = new List<ContextFile>();
        int usedTokens = 0;

        foreach (var file in ranked)
        {
            if (usedTokens + file.EstimatedTokens > tokenBudget)
                break;

            result.Add(file);
            usedTokens += file.EstimatedTokens;
        }

        return result;
    }

    private int EstimateFileTokens(string filePath)
    {
        var file = _fileIndex[filePath];
        return (int)(file.FileSize / 4); // Rough heuristic
    }
}
```

---

## 10. Implementation Architecture

### 10.1 Class Structure
```
MiniClaudeCode.Core/Indexing/
├── IndexingService.cs              (Main orchestrator)
├── Models/
│   ├── FileIndexEntry.cs
│   ├── SymbolIndexEntry.cs
│   ├── ImportReference.cs
│   └── SearchResult.cs
├── LanguageDetector.cs             (ILanguageDetector)
├── SymbolExtractors/
│   ├── ISymbolExtractor.cs
│   ├── RegexSymbolExtractor.cs     (Base class)
│   ├── CSharpSymbolExtractor.cs
│   ├── TypeScriptSymbolExtractor.cs
│   └── PythonSymbolExtractor.cs
├── ImportResolvers/
│   ├── IImportResolver.cs
│   ├── CSharpImportResolver.cs
│   ├── TypeScriptImportResolver.cs
│   └── PythonImportResolver.cs
├── Storage/
│   ├── IIndexStorage.cs
│   └── InMemoryIndexStorage.cs
├── Search/
│   ├── IFuzzyMatcher.cs
│   └── FuzzyMatcher.cs
└── Scoring/
    └── ContextRelevanceScorer.cs
```

### 10.2 IndexingService Lifecycle
```csharp
public class IndexingService : IDisposable
{
    private readonly WorkspaceService _workspace;
    private readonly FileWatcherService _fileWatcher;
    private readonly IIndexStorage _storage;
    private readonly ILanguageDetector _languageDetector;
    private readonly Dictionary<string, ISymbolExtractor> _extractors;
    private readonly ReaderWriterLockSlim _lock = new();
    private CancellationTokenSource? _indexingCts;

    public IndexingState State { get; private set; } = IndexingState.NotStarted;
    public event Action<IndexingProgress>? ProgressChanged;
    public event Action? IndexingCompleted;

    public IndexingService(
        WorkspaceService workspace,
        FileWatcherService fileWatcher,
        IIndexStorage storage,
        ILanguageDetector languageDetector,
        IEnumerable<ISymbolExtractor> extractors)
    {
        _workspace = workspace;
        _fileWatcher = fileWatcher;
        _storage = storage;
        _languageDetector = languageDetector;
        _extractors = extractors.ToDictionary(e => e.LanguageId);

        // Subscribe to workspace events
        _workspace.WorkspaceFoldersChanged += OnWorkspaceFoldersChanged;

        // Subscribe to file watcher events
        _fileWatcher.FilesChanged += OnFilesChanged;
        _fileWatcher.FileCreated += OnFileCreated;
        _fileWatcher.FileDeleted += OnFileDeleted;
        _fileWatcher.FileRenamed += OnFileRenamed;
    }

    private async void OnWorkspaceFoldersChanged()
    {
        await RebuildIndexAsync();
    }

    public async Task RebuildIndexAsync()
    {
        _indexingCts?.Cancel();
        _indexingCts = new CancellationTokenSource();

        State = IndexingState.Scanning;
        _storage.Clear();

        try
        {
            var allFiles = new List<string>();

            // Enumerate all workspace files
            foreach (var folder in _workspace.Folders)
            {
                var files = Directory.EnumerateFiles(
                    folder.ResolvedPath,
                    "*.*",
                    SearchOption.AllDirectories)
                    .Where(f => _languageDetector.ShouldIndex(f))
                    .ToList();

                allFiles.AddRange(files);
            }

            State = IndexingState.Indexing;
            int processed = 0;

            // Parallel indexing
            await Parallel.ForEachAsync(allFiles, _indexingCts.Token, async (file, ct) =>
            {
                await IndexFileAsync(file, ct);

                var current = Interlocked.Increment(ref processed);
                ProgressChanged?.Invoke(new IndexingProgress
                {
                    TotalFiles = allFiles.Count,
                    ProcessedFiles = current
                });
            });

            State = IndexingState.Ready;
            IndexingCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            State = IndexingState.Cancelled;
        }
    }

    private async Task IndexFileAsync(string filePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var languageId = _languageDetector.DetectLanguage(filePath);
        if (languageId == null) return;

        var content = await File.ReadAllTextAsync(filePath, ct);
        var contentHash = ComputeHash(content);

        var relativePath = GetRelativePath(filePath);

        // Add to file index
        _lock.EnterWriteLock();
        try
        {
            _storage.AddFile(new FileIndexEntry
            {
                FilePath = relativePath,
                AbsolutePath = filePath,
                LanguageId = languageId,
                FileSize = new FileInfo(filePath).Length,
                LastModified = File.GetLastWriteTimeUtc(filePath),
                ContentHash = contentHash,
                WorkspaceFolderIndex = GetWorkspaceFolderIndex(filePath)
            });
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        // Extract symbols
        if (_extractors.TryGetValue(languageId, out var extractor))
        {
            var symbols = extractor.ExtractSymbols(relativePath, content, languageId);

            _lock.EnterWriteLock();
            try
            {
                _storage.AddSymbols(relativePath, symbols);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }

    private async void OnFilesChanged(IReadOnlyList<string> files)
    {
        foreach (var file in files)
        {
            if (!_languageDetector.ShouldIndex(file)) continue;

            // Check if content actually changed (via hash)
            var entry = _storage.GetFile(GetRelativePath(file));
            if (entry != null)
            {
                var content = await File.ReadAllTextAsync(file);
                var newHash = ComputeHash(content);

                if (newHash == entry.ContentHash)
                    continue; // No actual change
            }

            await IndexFileAsync(file, CancellationToken.None);
        }
    }

    private void OnFileDeleted(string file)
    {
        var relativePath = GetRelativePath(file);

        _lock.EnterWriteLock();
        try
        {
            _storage.RemoveFile(relativePath);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // ... other helper methods
}
```

---

## 11. Acceptance Criteria

### 11.1 File Indexing (REQ-INDEX-001)
**Priority**: P0

**Given** a workspace with 1000 files
**When** the workspace is opened
**Then** the file index should be built within 3 seconds
**And** all indexed files should have correct language IDs
**And** ignored directories (`.git`, `node_modules`) should be excluded

---

### 11.2 Symbol Extraction (REQ-INDEX-002)
**Priority**: P0

**Given** a C# file with 10 classes and 50 methods
**When** the file is indexed
**Then** all classes should be extracted as `SymbolKind.Class`
**And** all methods should be extracted as `SymbolKind.Method`
**And** methods should reference their parent class in `ParentSymbol`
**And** line numbers should be accurate (±1 line tolerance for regex limitations)

---

### 11.3 Incremental Updates (REQ-INDEX-003)
**Priority**: P0

**Given** a workspace that is already indexed
**When** a single file is modified and saved
**Then** the index should update within 100ms
**And** only that file's symbols should be re-extracted
**And** other files' index entries should remain unchanged

---

### 11.4 File Search (REQ-SEARCH-001)
**Priority**: P0

**Given** an index with 10,000 files
**When** user searches for "myfile"
**Then** results should return within 10ms
**And** files with "myfile" as prefix should rank higher than substring matches
**And** at most 50 results should be returned

---

### 11.5 Symbol Search (REQ-SEARCH-002)
**Priority**: P0

**Given** an index with 50,000 symbols
**When** user searches for "MyMethod"
**Then** results should return within 20ms
**And** exact matches should rank highest
**And** results should include file path and line number

---

### 11.6 @ File Mention (REQ-MENTION-001)
**Priority**: P0

**Given** user types `@` in chat input
**When** the autocomplete dropdown appears
**Then** it should show files sorted by recent usage
**And** typing more characters should filter results in real-time
**And** selecting a file should insert `@filename.ext` into chat
**And** on sending the message, file content should be included in AI context

---

### 11.7 @ Symbol Mention (REQ-MENTION-002)
**Priority**: P1

**Given** user types `@#` in chat input
**When** the autocomplete dropdown appears
**Then** it should show symbols instead of files
**And** selecting a symbol should insert `@#ClassName.MethodName`
**And** on sending the message, symbol definition (±20 lines) should be included in AI context

---

### 11.8 Context Relevance Scoring (REQ-CONTEXT-001)
**Priority**: P1

**Given** user has `FileA.cs` open, which imports `FileB.cs`
**When** building AI context with 8000 token budget
**Then** `FileA.cs` should be ranked highest (score 100)
**And** `FileB.cs` should be ranked second (score 30)
**And** unrelated files should be excluded if they exceed token budget

---

### 11.9 Multi-Root Workspace Support (REQ-INDEX-004)
**Priority**: P1

**Given** a workspace with 3 root folders
**When** the workspace is indexed
**Then** all folders should be scanned
**And** files should be tagged with their workspace folder index
**And** search should work across all folders

---

### 11.10 Performance Under Load (REQ-PERF-001)
**Priority**: P0

**Given** a workspace with 100,000 files
**When** indexing starts
**Then** it should complete within 90 seconds
**And** memory usage should not exceed 500MB
**And** the UI should remain responsive (no freezing)

---

## 12. Risk & Assumptions Register

### 12.1 Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Regex parsing inaccurate** | High | Medium | Accept for MVP; plan Roslyn/TS Compiler API for v2 |
| **Large files cause memory issues** | Medium | High | Enforce 10MB file size limit; stream large files |
| **Indexing too slow on large repos** | Medium | High | Use parallel processing; show cancellation option |
| **Symbol extraction misses edge cases** | High | Low | Iterate on regex patterns based on user feedback |
| **Search performance degrades with scale** | Low | High | Benchmark with 100K files; optimize with parallel search |
| **Concurrency bugs (race conditions)** | Medium | High | Use `ReaderWriterLockSlim`; write comprehensive tests |
| **File watcher misses events** | Low | Medium | Provide manual "Refresh Index" command |

### 12.2 Assumptions

| Assumption | If Invalidated |
|------------|----------------|
| Regex parsing is acceptable for MVP | Switch to full AST parsing (Roslyn, TSC API) |
| Users primarily work with C#/TypeScript | Add more language extractors |
| In-memory index is fast enough | Implement SQLite persistence |
| Token budget of 8000 is reasonable | Make configurable per-model |
| File watcher is reliable | Implement periodic full re-scan (every 5 minutes) |
| Users won't have > 100K files | Implement distributed indexing or index sampling |

---

## 13. Terminology Dictionary

| Term | Definition |
|------|------------|
| **Codebase Index** | In-memory data structure storing metadata about workspace files, symbols, and imports |
| **Symbol** | A named code entity (class, method, property, function, etc.) |
| **Symbol Kind** | Category of symbol (Class, Method, Property, etc.) |
| **Fuzzy Matching** | Approximate string matching allowing partial/out-of-order matches |
| **@ Mention** | Inline reference to a file or symbol in chat input, prefixed with `@` |
| **Context** | Set of files/symbols included in AI prompt to provide relevant code information |
| **Token Budget** | Maximum number of tokens allowed in AI context (due to model limits) |
| **Relevance Score** | Numeric ranking of how relevant a file is to current editing context |
| **Incremental Update** | Updating only changed files in index, rather than full rebuild |
| **Import Graph** | Directed graph of file-to-file import relationships |
| **Language ID** | String identifier for programming language (e.g., "csharp", "typescript") |
| **Workspace Folder** | Root directory of a project (multi-root workspaces have multiple) |
| **File Watcher** | Service monitoring file system for changes (creates, deletes, modifications) |
| **Debouncing** | Delaying action until events stop firing for a period (avoid rapid-fire updates) |

---

## 14. Next Steps

### 14.1 Implementation Phases

**Phase 1 (MVP - Week 1-2)**:
- [ ] Implement `IndexingService` with file indexing only (no symbols)
- [ ] Integrate with `FileWatcherService` for incremental updates
- [ ] Implement fuzzy file search
- [ ] Implement `@` file mention completion
- [ ] Test with small workspace (100 files)

**Phase 2 (Symbol Support - Week 3)**:
- [ ] Implement regex-based symbol extraction for C#
- [ ] Add TypeScript symbol extraction
- [ ] Implement symbol search
- [ ] Implement `@#` symbol mention completion
- [ ] Test with medium workspace (1000 files)

**Phase 3 (Context Ranking - Week 4)**:
- [ ] Implement import/reference tracking
- [ ] Implement context relevance scoring
- [ ] Integrate with AI chat for context injection
- [ ] Test with large workspace (10K files)

**Phase 4 (Polish & Performance - Week 5)**:
- [ ] Optimize search performance (parallel, caching)
- [ ] Add progress notifications
- [ ] Add error handling and recovery
- [ ] Benchmark with 100K files
- [ ] Write comprehensive test suite

### 14.2 Open Questions for Stakeholders

1. **Persistence**: Should we add SQLite persistence in Phase 1, or defer to Phase 5?
   **Recommendation**: Defer to Phase 5 (after MVP validation)

2. **Language Priority**: Focus on C# first, or implement C#/TS/Python in parallel?
   **Recommendation**: C# first (Phase 2), TS in Phase 3, Python in Phase 4

3. **UI Integration**: Where should @ mention dropdown be rendered (chat input only, or also in editor)?
   **Recommendation**: Chat input only for MVP

4. **Token Budget**: Should this be configurable per-model, or hardcoded?
   **Recommendation**: Configurable (expose in settings)

### 14.3 Success Metrics

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Indexing Speed (10K files) | < 8s | Automated benchmark |
| Search Latency | < 10ms | Instrumented timer |
| Memory Usage (10K files) | < 50MB | Process memory profiler |
| User Adoption (@ mentions) | > 50% of messages | Telemetry (if enabled) |
| Crash Rate | < 1% of sessions | Error logging |

---

**End of Specification**

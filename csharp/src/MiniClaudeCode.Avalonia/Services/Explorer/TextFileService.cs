using System.Collections.Concurrent;
using MiniClaudeCode.Avalonia.Editor;
using MiniClaudeCode.Avalonia.Editor.TextBuffer;
using MiniClaudeCode.Avalonia.Logging;
using MiniClaudeCode.Avalonia.Models;

namespace MiniClaudeCode.Avalonia.Services.Explorer;

/// <summary>
/// Implementation of ITextFileService with model caching and reference counting.
/// Maps to VSCode's TextFileService + TextModelService (Sections 3.3, 6.3).
/// 
/// Manages the lifecycle of TextFileModel instances:
/// - Caches models by file path
/// - Uses reference counting for disposal (doc 8.1 step 4)
/// - Supports backup → write → cleanup → event save flow (doc 7.2)
/// </summary>
public class TextFileService : ITextFileService
{
    private readonly ConcurrentDictionary<string, TextFileModel> _modelCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileBackupService? _backupService;

    /// <inheritdoc />
    public event Action<TextFileModelChangeEvent>? OnDidChangeDirty;

    /// <inheritdoc />
    public event Action<TextFileSaveEvent>? OnDidSave;

    /// <inheritdoc />
    public event Action<TextFileLoadEvent>? OnDidLoad;

    /// <inheritdoc />
    public event Func<string, Task>? OnWillSave;

    public TextFileService(FileBackupService? backupService = null)
    {
        _backupService = backupService;
    }

    /// <inheritdoc />
    public TextFileModel? GetModel(string filePath)
    {
        _modelCache.TryGetValue(filePath, out var model);
        return model;
    }

    /// <inheritdoc />
    public async Task<TextFileModel> ResolveAsync(string filePath)
    {
        LogHelper.UI.Info("[Preview链路] TextFileService.ResolveAsync: 开始, Path={0}", filePath);

        // Check cache first (doc 6.3 step 1)
        if (_modelCache.TryGetValue(filePath, out var existing))
        {
            existing.ReferenceCount++;
            LogHelper.UI.Debug("[Preview链路] TextFileService.ResolveAsync: 缓存命中, RefCount={0}", existing.ReferenceCount);
            return existing;
        }

        LogHelper.UI.Debug("[Preview链路] TextFileService.ResolveAsync: 缓存未命中, 从磁盘加载");
        // Create new model
        var model = new TextFileModel(filePath);

        // Read file content (doc 6.3 step 2)
        string content;
        PieceTreeTextBuffer? textBuffer = null;

        try
        {
            var fileInfo = new FileInfo(filePath);

            if (fileInfo.Length > LargeFileConstants.LargeFileSizeThreshold)
            {
                // Large file: use PieceTree buffer
                textBuffer = await PieceTreeTextBufferBuilder.LoadFileAsync(filePath);
                var viewportLines = 200;
                var sb = new System.Text.StringBuilder();
                for (int line = 1; line <= Math.Min(viewportLines, textBuffer.LineCount); line++)
                {
                    if (line > 1) sb.Append('\n');
                    sb.Append(textBuffer.GetLineContent(line));
                }
                content = sb.ToString();
                model.TextBuffer = textBuffer;
            }
            else
            {
                content = await File.ReadAllTextAsync(filePath);
            }

            model.Content = content;
            model.IsResolved = true;

            // Update disk metadata (doc 5.4)
            model.UpdateDiskMetadata(content, fileInfo.LastWriteTimeUtc);
        }
        catch (Exception ex)
        {
            LogHelper.UI.Error(ex, "[Preview链路] TextFileService.ResolveAsync: 加载失败, Path={0}", filePath);
            model.Content = $"Error loading file: {ex.Message}";
            model.IsResolved = false;
            content = model.Content;
        }

        // Wire up events (doc 6.3 step 6)
        model.OnDidChangeDirty += evt => OnDidChangeDirty?.Invoke(evt);
        model.OnDidSave += evt => OnDidSave?.Invoke(evt);
        model.OnWillDispose += m =>
        {
            _modelCache.TryRemove(m.Resource, out _);
        };

        // Cache model (doc 6.3 step 5)
        model.ReferenceCount = 1;
        _modelCache[filePath] = model;

        // Fire load event
        OnDidLoad?.Invoke(new TextFileLoadEvent(filePath, content));

        LogHelper.UI.Info("[Preview链路] TextFileService.ResolveAsync: 完成, Path={0}, ContentLength={1}", filePath, content.Length);
        return model;
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(string filePath, SaveOptions? options = null)
    {
        if (!_modelCache.TryGetValue(filePath, out var model))
            return false;

        if (!model.IsDirty)
            return true;

        options ??= new SaveOptions();

        // Step 3: Fire onWillSave event (doc 7.2)
        if (OnWillSave != null)
        {
            await OnWillSave.Invoke(filePath);
        }

        // Step 4: Create backup (doc 7.2)
        if (options.CreateBackup)
        {
            _backupService?.CreateBackup(filePath);
        }

        try
        {
            // Step 5: Write to disk (doc 7.2)
            if (model.TextBuffer != null)
            {
                await using var writer = new StreamWriter(filePath);
                foreach (var chunk in model.TextBuffer.CreateSnapshot())
                    await writer.WriteAsync(chunk);
            }
            else
            {
                await File.WriteAllTextAsync(filePath, model.Content);
            }

            // Step 6: Update model state (doc 7.2)
            model.MarkSaved();

            // Update disk metadata
            try
            {
                var fi = new FileInfo(filePath);
                model.UpdateDiskMetadata(model.Content, fi.LastWriteTimeUtc);
            }
            catch { /* ignore metadata update failure */ }

            return true;
        }
        catch
        {
            // Save failed -- backup is retained for recovery
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RevertAsync(string filePath)
    {
        if (!_modelCache.TryGetValue(filePath, out var model))
            return false;

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            model.Revert(content);

            // Update disk metadata
            var fi = new FileInfo(filePath);
            model.UpdateDiskMetadata(content, fi.LastWriteTimeUtc);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void ReleaseModel(string filePath)
    {
        if (!_modelCache.TryGetValue(filePath, out var model))
            return;

        model.ReferenceCount--;

        if (model.ReferenceCount <= 0)
        {
            model.Dispose();
            _modelCache.TryRemove(filePath, out _);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetDirtyFiles()
    {
        return _modelCache.Values
            .Where(m => m.IsDirty)
            .Select(m => m.Resource)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ExternalChangeResult> HandleExternalChangeAsync(string filePath)
    {
        if (!_modelCache.TryGetValue(filePath, out var model))
            return ExternalChangeResult.NoChange;

        if (!File.Exists(filePath))
        {
            HandleExternalDeletion(filePath);
            return ExternalChangeResult.NoChange;
        }

        try
        {
            var fi = new FileInfo(filePath);

            // Skip if modification time hasn't changed
            if (model.DiskModifiedTime.HasValue &&
                fi.LastWriteTimeUtc <= model.DiskModifiedTime.Value)
                return ExternalChangeResult.NoChange;

            var diskContent = await File.ReadAllTextAsync(filePath);

            // Use hash for precise comparison (doc 5.4)
            var diskHash = TextFileModel.ComputeHash(diskContent);
            if (diskHash == model.DiskHash)
                return ExternalChangeResult.NoChange;

            if (!model.IsDirty)
            {
                // File is clean: silently reload (doc 5.4)
                model.Revert(diskContent);
                model.UpdateDiskMetadata(diskContent, fi.LastWriteTimeUtc);
                return ExternalChangeResult.AutoReloaded;
            }
            else
            {
                // File is dirty: conflict! (doc 5.4)
                return ExternalChangeResult.Conflict;
            }
        }
        catch
        {
            return ExternalChangeResult.Error;
        }
    }

    /// <inheritdoc />
    public void HandleExternalDeletion(string filePath)
    {
        if (!_modelCache.TryGetValue(filePath, out var model))
            return;

        model.IsOrphaned = true;

        // If not dirty, mark as dirty so save-on-close prompts
        if (!model.IsDirty)
        {
            model.CurrentVersionId++;
            OnDidChangeDirty?.Invoke(new TextFileModelChangeEvent(filePath, true));
        }
    }
}

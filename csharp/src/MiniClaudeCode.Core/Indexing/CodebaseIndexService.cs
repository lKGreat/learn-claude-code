using System.Collections.Concurrent;
using MiniClaudeCode.Abstractions.Indexing;

namespace MiniClaudeCode.Core.Indexing;

/// <summary>
/// Manages file-level indexing of the codebase for fast file search and navigation.
/// Implements in-memory index with fuzzy search and incremental updates.
/// </summary>
public class CodebaseIndexService : ICodebaseIndex
{
    private readonly ConcurrentDictionary<string, FileIndexEntry> _fileIndex = new();
    private readonly HashSet<string> _excludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".vs", ".vscode",
        "dist", "build", "out", "target", ".idea", "__pycache__", ".next"
    };
    private readonly HashSet<string> _excludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".so", ".dylib", ".a", ".lib", ".obj", ".o",
        ".zip", ".tar", ".gz", ".rar", ".7z",
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".svg",
        ".mp3", ".mp4", ".avi", ".mov", ".wmv",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
    };
    private const long MaxFileSizeBytes = 1024 * 1024; // 1MB
    private string? _rootPath;

    public bool IsIndexed { get; private set; }
    public int FileCount => _fileIndex.Count;

    public async Task IndexWorkspaceAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Root path does not exist: {rootPath}");
        }

        _rootPath = Path.GetFullPath(rootPath);
        _fileIndex.Clear();

        var files = EnumerateIndexableFiles(_rootPath);

        // Parallel file scanning for performance
        await Parallel.ForEachAsync(
            files,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (filePath, ct) =>
            {
                try
                {
                    var entry = await CreateFileIndexEntryAsync(filePath, ct);
                    if (entry != null)
                    {
                        _fileIndex[filePath] = entry;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Skip files that fail to index
                    Console.Error.WriteLine($"Failed to index {filePath}: {ex.Message}");
                }
            });

        IsIndexed = true;
    }

    public Task<IReadOnlyList<FileIndexEntry>> SearchFilesAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<FileIndexEntry>>(Array.Empty<FileIndexEntry>());
        }

        // Fuzzy search with scoring
        var results = _fileIndex.Values
            .Select(entry => (entry, score: CalculateFuzzyScore(query, entry.Path)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(limit)
            .Select(x => x.entry with { RelevanceScore = x.score })
            .ToList();

        return Task.FromResult<IReadOnlyList<FileIndexEntry>>(results);
    }

    public Task<FileIndexEntry?> GetFileInfoAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        _fileIndex.TryGetValue(normalizedPath, out var entry);
        return Task.FromResult(entry);
    }

    public async Task OnFileChangedAsync(
        string filePath,
        FileChangeType changeType,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = Path.GetFullPath(filePath);

        switch (changeType)
        {
            case FileChangeType.Created:
            case FileChangeType.Modified:
                if (IsIndexableFile(normalizedPath))
                {
                    var entry = await CreateFileIndexEntryAsync(normalizedPath, cancellationToken);
                    if (entry != null)
                    {
                        _fileIndex[normalizedPath] = entry;
                    }
                }
                break;

            case FileChangeType.Deleted:
                _fileIndex.TryRemove(normalizedPath, out _);
                break;
        }
    }

    private IEnumerable<string> EnumerateIndexableFiles(string rootPath)
    {
        var queue = new Queue<string>();
        queue.Enqueue(rootPath);

        while (queue.Count > 0)
        {
            var currentDir = queue.Dequeue();

            // Get subdirectories
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(currentDir))
                {
                    var dirName = Path.GetFileName(dir);
                    if (!_excludedDirectories.Contains(dirName))
                    {
                        queue.Enqueue(dir);
                    }
                }
            }
            catch
            {
                // Skip directories we can't access
                continue;
            }

            // Get files
            string[] files;
            try
            {
                files = Directory.GetFiles(currentDir);
            }
            catch
            {
                // Skip directories we can't access
                continue;
            }

            foreach (var file in files)
            {
                if (IsIndexableFile(file))
                {
                    yield return file;
                }
            }
        }
    }

    private bool IsIndexableFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            var ext = Path.GetExtension(filePath);
            if (_excludedExtensions.Contains(ext))
                return false;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSizeBytes)
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<FileIndexEntry?> CreateFileIndexEntryAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists || fileInfo.Length > MaxFileSizeBytes)
            {
                return null;
            }

            var language = DetectLanguage(filePath);

            return new FileIndexEntry
            {
                Path = filePath,
                Language = language,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc,
                Symbols = null, // Populated by SymbolIndexService
                RelevanceScore = 0.0
            };
        }
        catch
        {
            return null;
        }
    }

    private static string DetectLanguage(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".tsx" => "typescript",
            ".jsx" => "javascript",
            ".py" => "python",
            ".java" => "java",
            ".cpp" or ".cc" or ".cxx" => "cpp",
            ".c" => "c",
            ".h" or ".hpp" => "cpp",
            ".rs" => "rust",
            ".go" => "go",
            ".rb" => "ruby",
            ".php" => "php",
            ".swift" => "swift",
            ".kt" or ".kts" => "kotlin",
            ".scala" => "scala",
            ".sh" => "bash",
            ".ps1" => "powershell",
            ".sql" => "sql",
            ".html" or ".htm" => "html",
            ".css" => "css",
            ".scss" or ".sass" => "scss",
            ".json" => "json",
            ".xml" => "xml",
            ".yaml" or ".yml" => "yaml",
            ".md" => "markdown",
            ".tex" => "latex",
            _ => "plaintext"
        };
    }

    /// <summary>
    /// Calculate fuzzy match score using subsequence matching.
    /// Higher scores for consecutive matches, word boundaries, and shorter paths.
    /// </summary>
    private static double CalculateFuzzyScore(string query, string target)
    {
        query = query.ToLowerInvariant();
        target = Path.GetFileName(target).ToLowerInvariant();

        if (target.Contains(query))
        {
            // Exact substring match gets highest score
            return target == query ? 1.0 : 0.9;
        }

        // Subsequence matching
        int queryIndex = 0;
        int consecutiveMatches = 0;
        int maxConsecutive = 0;
        int wordBoundaryMatches = 0;
        bool lastWasSeparator = true;

        for (int i = 0; i < target.Length && queryIndex < query.Length; i++)
        {
            if (target[i] == query[queryIndex])
            {
                queryIndex++;
                consecutiveMatches++;
                maxConsecutive = Math.Max(maxConsecutive, consecutiveMatches);

                if (lastWasSeparator)
                {
                    wordBoundaryMatches++;
                }
            }
            else
            {
                consecutiveMatches = 0;
            }

            lastWasSeparator = target[i] == '/' || target[i] == '\\' ||
                               target[i] == '_' || target[i] == '-' ||
                               target[i] == '.';
        }

        // Not a match if we didn't consume all query characters
        if (queryIndex < query.Length)
        {
            return 0;
        }

        // Score calculation:
        // - Base score for subsequence match: 0.3
        // - Bonus for consecutive characters: up to 0.4
        // - Bonus for word boundary matches: up to 0.2
        // - Bonus for shorter paths: up to 0.1
        double score = 0.3;
        score += (double)maxConsecutive / query.Length * 0.4;
        score += (double)wordBoundaryMatches / query.Length * 0.2;
        score += (1.0 - Math.Min(target.Length / 100.0, 1.0)) * 0.1;

        return score;
    }
}

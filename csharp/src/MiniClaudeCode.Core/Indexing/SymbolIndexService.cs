using System.Collections.Concurrent;
using MiniClaudeCode.Abstractions.Indexing;

namespace MiniClaudeCode.Core.Indexing;

/// <summary>
/// Manages symbol-level indexing of the codebase (classes, methods, properties, etc.).
/// Implements lazy parsing with caching and invalidation on file changes.
/// </summary>
public class SymbolIndexService : ISymbolIndex
{
    private readonly ICodebaseIndex _codebaseIndex;
    private readonly ConcurrentDictionary<string, IReadOnlyList<SymbolInfo>> _symbolCache = new();
    private readonly object _cacheLock = new();

    public SymbolIndexService(ICodebaseIndex codebaseIndex)
    {
        _codebaseIndex = codebaseIndex ?? throw new ArgumentNullException(nameof(codebaseIndex));
    }

    public async Task<IReadOnlyList<SymbolInfo>> GetSymbolsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = Path.GetFullPath(filePath);

        // Check cache first
        if (_symbolCache.TryGetValue(normalizedPath, out var cachedSymbols))
        {
            return cachedSymbols;
        }

        // Parse and cache
        var symbols = await ParseFileSymbolsAsync(normalizedPath, cancellationToken);
        _symbolCache[normalizedPath] = symbols;
        return symbols;
    }

    public async Task<IReadOnlyList<SymbolInfo>> SearchSymbolsAsync(
        string query,
        int limit = 20,
        SymbolKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SymbolInfo>();
        }

        // Ensure we have indexed files
        if (!_codebaseIndex.IsIndexed)
        {
            return Array.Empty<SymbolInfo>();
        }

        // Get all cached symbols or parse on demand
        var allSymbols = await GetAllSymbolsAsync(cancellationToken);

        // Filter by kind if specified
        var filteredSymbols = kind.HasValue
            ? allSymbols.Where(s => s.Kind == kind.Value)
            : allSymbols;

        // Fuzzy search with scoring
        var results = filteredSymbols
            .Select(symbol => (symbol, score: CalculateFuzzyScore(query, symbol.Name)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(limit)
            .Select(x => x.symbol with { RelevanceScore = x.score })
            .ToList();

        return results;
    }

    public async Task<SymbolInfo?> GetSymbolAsync(
        string fullyQualifiedName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedName))
        {
            return null;
        }

        var allSymbols = await GetAllSymbolsAsync(cancellationToken);
        return allSymbols.FirstOrDefault(s =>
            s.FullyQualifiedName.Equals(fullyQualifiedName, StringComparison.Ordinal));
    }

    public async Task<IReadOnlyList<SymbolReference>> FindReferencesAsync(
        string fullyQualifiedName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullyQualifiedName))
        {
            return Array.Empty<SymbolReference>();
        }

        // Get the symbol to find its simple name
        var symbol = await GetSymbolAsync(fullyQualifiedName, cancellationToken);
        if (symbol == null)
        {
            return Array.Empty<SymbolReference>();
        }

        var references = new List<SymbolReference>();

        // Get all indexed files
        var allFiles = await _codebaseIndex.SearchFilesAsync("", limit: int.MaxValue, cancellationToken);

        // Search for the symbol name in each file
        await Parallel.ForEachAsync(
            allFiles,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (fileEntry, ct) =>
            {
                try
                {
                    var fileReferences = await FindReferencesInFileAsync(
                        fileEntry.Path,
                        symbol.Name,
                        symbol.FilePath,
                        symbol.Line,
                        ct);

                    lock (references)
                    {
                        references.AddRange(fileReferences);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Skip files that fail to search
                }
            });

        return references;
    }

    /// <summary>
    /// Invalidate the symbol cache for a file when it changes.
    /// Should be called when file system watchers detect changes.
    /// </summary>
    public void InvalidateFile(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        _symbolCache.TryRemove(normalizedPath, out _);
    }

    private async Task<IReadOnlyList<SymbolInfo>> ParseFileSymbolsAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return Array.Empty<SymbolInfo>();
            }

            var fileInfo = await _codebaseIndex.GetFileInfoAsync(filePath, cancellationToken);
            if (fileInfo == null)
            {
                return Array.Empty<SymbolInfo>();
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var symbols = SymbolParser.ParseSymbols(filePath, content, fileInfo.Language);

            return symbols;
        }
        catch
        {
            return Array.Empty<SymbolInfo>();
        }
    }

    private async Task<IReadOnlyList<SymbolInfo>> GetAllSymbolsAsync(CancellationToken cancellationToken)
    {
        var allSymbols = new List<SymbolInfo>();

        // Get all indexed files
        var allFiles = await _codebaseIndex.SearchFilesAsync("", limit: int.MaxValue, cancellationToken);

        // Parse symbols from each file (using cache when available)
        await Parallel.ForEachAsync(
            allFiles,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (fileEntry, ct) =>
            {
                var symbols = await GetSymbolsAsync(fileEntry.Path, ct);
                lock (allSymbols)
                {
                    allSymbols.AddRange(symbols);
                }
            });

        return allSymbols;
    }

    private async Task<List<SymbolReference>> FindReferencesInFileAsync(
        string filePath,
        string symbolName,
        string definitionFilePath,
        int definitionLine,
        CancellationToken cancellationToken)
    {
        var references = new List<SymbolReference>();

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var index = 0;

                while ((index = line.IndexOf(symbolName, index, StringComparison.Ordinal)) >= 0)
                {
                    // Simple heuristic: check if it's a word boundary
                    bool isWordBoundary = true;
                    if (index > 0)
                    {
                        var charBefore = line[index - 1];
                        isWordBoundary &= !char.IsLetterOrDigit(charBefore) && charBefore != '_';
                    }
                    if (index + symbolName.Length < line.Length)
                    {
                        var charAfter = line[index + symbolName.Length];
                        isWordBoundary &= !char.IsLetterOrDigit(charAfter) && charAfter != '_';
                    }

                    if (isWordBoundary)
                    {
                        bool isDefinition = filePath == definitionFilePath && i == definitionLine;

                        references.Add(new SymbolReference
                        {
                            FilePath = filePath,
                            Line = i,
                            Column = index,
                            LineContent = line.Trim(),
                            IsDefinition = isDefinition
                        });
                    }

                    index += symbolName.Length;
                }
            }
        }
        catch
        {
            // Skip files that fail to read
        }

        return references;
    }

    /// <summary>
    /// Calculate fuzzy match score using subsequence matching.
    /// Similar algorithm to file search but optimized for symbol names.
    /// </summary>
    private static double CalculateFuzzyScore(string query, string target)
    {
        query = query.ToLowerInvariant();
        target = target.ToLowerInvariant();

        // Exact match
        if (target == query)
            return 1.0;

        // Starts with query
        if (target.StartsWith(query))
            return 0.95;

        // Contains query
        if (target.Contains(query))
            return 0.85;

        // Subsequence matching
        int queryIndex = 0;
        int consecutiveMatches = 0;
        int maxConsecutive = 0;
        int camelCaseMatches = 0;
        bool lastWasUpper = false;

        for (int i = 0; i < target.Length && queryIndex < query.Length; i++)
        {
            if (target[i] == query[queryIndex])
            {
                queryIndex++;
                consecutiveMatches++;
                maxConsecutive = Math.Max(maxConsecutive, consecutiveMatches);

                // Bonus for matching at camelCase boundaries
                if (char.IsUpper(target[i]) && i > 0 && !lastWasUpper)
                {
                    camelCaseMatches++;
                }
            }
            else
            {
                consecutiveMatches = 0;
            }

            lastWasUpper = char.IsUpper(target[i]);
        }

        // Not a match if we didn't consume all query characters
        if (queryIndex < query.Length)
            return 0;

        // Score calculation
        double score = 0.3; // Base subsequence match
        score += (double)maxConsecutive / query.Length * 0.4; // Consecutive bonus
        score += (double)camelCaseMatches / query.Length * 0.2; // CamelCase bonus
        score += (1.0 - Math.Min(target.Length / 50.0, 1.0)) * 0.1; // Length bonus

        return score;
    }
}

using System.Text;

namespace MiniClaudeCode.Core.AI;

/// <summary>
/// Builds contextual information for AI operations with token budget management.
/// Different context strategies for different operations: completion, chat, edit.
/// </summary>
public class ContextBuilder
{
    private readonly string _workDir;

    /// <summary>
    /// Token budget configuration.
    /// </summary>
    public ContextBudgetConfig BudgetConfig { get; set; } = new();

    public ContextBuilder(string workDir)
    {
        _workDir = workDir;
    }

    /// <summary>
    /// Build lightweight context for code completion.
    /// Strategy: Current file ± 100 lines around cursor + open file names.
    /// </summary>
    public async Task<CompletionContext> BuildCompletionContextAsync(
        string filePath,
        int cursorLine,
        int cursorColumn,
        string[] openFiles,
        CancellationToken cancellationToken = default)
    {
        var context = new CompletionContext
        {
            FilePath = filePath,
            Language = DetectLanguage(filePath),
            CursorLine = cursorLine,
            CursorColumn = cursorColumn
        };

        try
        {
            var fullPath = Path.Combine(_workDir, filePath);
            if (!File.Exists(fullPath))
            {
                return context;
            }

            var allLines = await File.ReadAllLinesAsync(fullPath, cancellationToken);

            // Extract window around cursor
            int windowSize = 100;
            int startLine = Math.Max(0, cursorLine - windowSize);
            int endLine = Math.Min(allLines.Length, cursorLine + windowSize);

            context.CodeBefore = string.Join('\n', allLines[startLine..cursorLine]);
            context.CodeAfter = string.Join('\n', allLines[cursorLine..endLine]);

            // Add open file names for context
            context.OpenFiles = openFiles
                .Where(f => f != filePath)
                .Take(10)
                .ToList();

            context.EstimatedTokens = EstimateTokens(context.CodeBefore + context.CodeAfter);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Context build error: {ex.Message}");
        }

        return context;
    }

    /// <summary>
    /// Build comprehensive context for chat operations.
    /// Strategy: @mentioned files + current file + relevant symbols.
    /// Token budget: 60% main content, 30% context files, 10% system prompt.
    /// </summary>
    public async Task<ChatContext> BuildChatContextAsync(
        string currentFile,
        string[] mentionedFiles,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var context = new ChatContext
        {
            CurrentFile = currentFile,
            UserMessage = userMessage
        };

        var totalBudget = BudgetConfig.MaxContextTokens;
        var systemBudget = (int)(totalBudget * 0.1);
        var mainContentBudget = (int)(totalBudget * 0.6);
        var contextFilesBudget = (int)(totalBudget * 0.3);

        try
        {
            // Load current file
            if (!string.IsNullOrEmpty(currentFile))
            {
                var content = await LoadFileWithBudgetAsync(currentFile, mainContentBudget / 2, cancellationToken);
                context.CurrentFileContent = content;
            }

            // Load mentioned files
            var mentionedContents = new Dictionary<string, string>();
            int budgetPerFile = mentionedFiles.Length > 0 ? contextFilesBudget / mentionedFiles.Length : 0;

            foreach (var file in mentionedFiles)
            {
                var content = await LoadFileWithBudgetAsync(file, budgetPerFile, cancellationToken);
                mentionedContents[file] = content;
            }

            context.MentionedFiles = mentionedContents;

            // Estimate total tokens
            context.EstimatedTokens = EstimateTokens(
                context.CurrentFileContent +
                string.Join("", context.MentionedFiles.Values) +
                userMessage);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Chat context build error: {ex.Message}");
        }

        return context;
    }

    /// <summary>
    /// Build medium-weight context for inline editing.
    /// Strategy: Selected code + full file + import context.
    /// </summary>
    public async Task<EditContext> BuildEditContextAsync(
        string filePath,
        int startLine,
        int endLine,
        CancellationToken cancellationToken = default)
    {
        var context = new EditContext
        {
            FilePath = filePath,
            Language = DetectLanguage(filePath),
            StartLine = startLine,
            EndLine = endLine
        };

        try
        {
            var fullPath = Path.Combine(_workDir, filePath);
            if (!File.Exists(fullPath))
            {
                return context;
            }

            var allLines = await File.ReadAllLinesAsync(fullPath, cancellationToken);

            // Extract selected code
            if (startLine >= 0 && endLine < allLines.Length)
            {
                context.SelectedCode = string.Join('\n', allLines[startLine..(endLine + 1)]);
            }

            // Extract context before (up to 50 lines)
            int contextStartBefore = Math.Max(0, startLine - 50);
            context.ContextBefore = string.Join('\n', allLines[contextStartBefore..startLine]);

            // Extract context after (up to 50 lines)
            int contextEndAfter = Math.Min(allLines.Length, endLine + 1 + 50);
            context.ContextAfter = string.Join('\n', allLines[(endLine + 1)..contextEndAfter]);

            // Load full file for reference (truncated if too large)
            context.FullFileContent = await LoadFileWithBudgetAsync(
                filePath,
                BudgetConfig.MaxEditContextTokens,
                cancellationToken);

            context.EstimatedTokens = EstimateTokens(
                context.SelectedCode +
                context.ContextBefore +
                context.ContextAfter);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Edit context build error: {ex.Message}");
        }

        return context;
    }

    /// <summary>
    /// Load a file with token budget constraints.
    /// Truncates from the middle if necessary to stay within budget.
    /// </summary>
    private async Task<string> LoadFileWithBudgetAsync(
        string relativePath,
        int tokenBudget,
        CancellationToken cancellationToken)
    {
        try
        {
            var fullPath = Path.Combine(_workDir, relativePath);
            if (!File.Exists(fullPath))
            {
                return $"(file not found: {relativePath})";
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);

            // Check if within budget
            var estimatedTokens = EstimateTokens(content);
            if (estimatedTokens <= tokenBudget)
            {
                return content;
            }

            // Truncate: keep beginning and end, omit middle
            var charBudget = tokenBudget * 4; // Rough chars-to-tokens ratio
            int keepChars = charBudget / 2;

            if (content.Length <= keepChars * 2)
            {
                return content;
            }

            var beginning = content[..keepChars];
            var end = content[^keepChars..];
            var omittedChars = content.Length - (keepChars * 2);

            return $"{beginning}\n\n... ({omittedChars} characters omitted) ...\n\n{end}";
        }
        catch (Exception ex)
        {
            return $"(error loading {relativePath}: {ex.Message})";
        }
    }

    /// <summary>
    /// Estimate token count from text.
    /// Uses a simple heuristic: chars / 4.
    /// For production, consider using a proper tokenizer (tiktoken).
    /// </summary>
    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Rough estimate: 1 token ≈ 4 characters for English text
        // Code is slightly denser, so use 3.5
        return (int)(text.Length / 3.5);
    }

    /// <summary>
    /// Detect programming language from file extension.
    /// </summary>
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
}

/// <summary>
/// Token budget configuration.
/// </summary>
public class ContextBudgetConfig
{
    /// <summary>Maximum tokens for chat context.</summary>
    public int MaxContextTokens { get; set; } = 8000;

    /// <summary>Maximum tokens for edit context.</summary>
    public int MaxEditContextTokens { get; set; } = 4000;

    /// <summary>Maximum tokens for completion context (smaller for speed).</summary>
    public int MaxCompletionContextTokens { get; set; } = 2000;
}

/// <summary>
/// Context for code completion.
/// </summary>
public class CompletionContext
{
    public required string FilePath { get; init; }
    public required string Language { get; init; }
    public required int CursorLine { get; init; }
    public required int CursorColumn { get; init; }
    public string CodeBefore { get; set; } = "";
    public string CodeAfter { get; set; } = "";
    public List<string> OpenFiles { get; set; } = new();
    public int EstimatedTokens { get; set; }
}

/// <summary>
/// Context for chat operations.
/// </summary>
public class ChatContext
{
    public required string CurrentFile { get; init; }
    public required string UserMessage { get; init; }
    public string CurrentFileContent { get; set; } = "";
    public Dictionary<string, string> MentionedFiles { get; set; } = new();
    public int EstimatedTokens { get; set; }
}

/// <summary>
/// Context for inline editing.
/// </summary>
public class EditContext
{
    public required string FilePath { get; init; }
    public required string Language { get; init; }
    public required int StartLine { get; init; }
    public required int EndLine { get; init; }
    public string SelectedCode { get; set; } = "";
    public string ContextBefore { get; set; } = "";
    public string ContextAfter { get; set; } = "";
    public string FullFileContent { get; set; } = "";
    public int EstimatedTokens { get; set; }
}

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using MiniClaudeCode.Core.Configuration;
using MiniClaudeCode.Core.Services.Providers;

namespace MiniClaudeCode.Core.AI;

/// <summary>
/// High-performance inline code completion service with debouncing and caching.
/// Optimized for low-latency autocomplete suggestions as the user types.
/// </summary>
public class InlineCompletionService : IAsyncDisposable
{
    private readonly Dictionary<ModelProvider, ModelProviderConfig> _providerConfigs;
    private readonly ModelProvider _defaultProvider;
    private readonly ConcurrentDictionary<string, CachedCompletion> _cache = new();
    private readonly SemaphoreSlim _debounceLock = new(1, 1);
    private readonly TimeSpan _debounceDelay;
    private CancellationTokenSource? _pendingCancellation;

    /// <summary>
    /// Configuration for completion behavior.
    /// </summary>
    public InlineCompletionConfig Config { get; set; } = new();

    public InlineCompletionService(
        Dictionary<ModelProvider, ModelProviderConfig> providerConfigs,
        ModelProvider defaultProvider,
        TimeSpan? debounceDelay = null)
    {
        _providerConfigs = providerConfigs;
        _defaultProvider = defaultProvider;
        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(800);
    }

    /// <summary>
    /// Request a code completion with automatic debouncing.
    /// If multiple requests arrive rapidly, only the latest is processed.
    /// </summary>
    public async Task<CompletionResult?> GetCompletionAsync(
        CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Cancel any pending completion request
        _pendingCancellation?.Cancel();
        _pendingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Debounce: wait for typing to settle
            await Task.Delay(_debounceDelay, _pendingCancellation.Token);
        }
        catch (TaskCanceledException)
        {
            // Typing is still happening, this request is superseded
            return null;
        }

        // Check cache first
        var cacheKey = GenerateCacheKey(request);
        if (_cache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired(Config.CacheTTL))
        {
            return cached.Result;
        }

        // Acquire lock to prevent concurrent identical requests
        await _debounceLock.WaitAsync(_pendingCancellation.Token);
        try
        {
            // Double-check cache after acquiring lock
            if (_cache.TryGetValue(cacheKey, out cached) && !cached.IsExpired(Config.CacheTTL))
            {
                return cached.Result;
            }

            // Generate completion
            var result = await GenerateCompletionAsync(request, _pendingCancellation.Token);

            // Cache the result
            if (result != null)
            {
                _cache[cacheKey] = new CachedCompletion(result, DateTimeOffset.UtcNow);

                // Limit cache size
                if (_cache.Count > Config.MaxCacheSize)
                {
                    var oldestKey = _cache
                        .OrderBy(kvp => kvp.Value.Timestamp)
                        .First()
                        .Key;
                    _cache.TryRemove(oldestKey, out _);
                }
            }

            return result;
        }
        finally
        {
            _debounceLock.Release();
        }
    }

    /// <summary>
    /// Generate a completion using the LLM.
    /// Uses direct IChatCompletionService for minimal overhead.
    /// </summary>
    private async Task<CompletionResult?> GenerateCompletionAsync(
        CompletionRequest request,
        CancellationToken cancellationToken)
    {
        var providerConfig = GetProviderConfig();
        var kernel = BuildLightweightKernel(providerConfig);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        // Build prompt with context
        var prompt = BuildCompletionPrompt(request);

        var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = Config.Temperature,
            MaxTokens = Config.MaxTokens,
            TopP = 0.95,
            FrequencyPenalty = 0.0,
            PresencePenalty = 0.0
        };

        try
        {
            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                kernel,
                cancellationToken);

            var completionText = response.Content?.Trim() ?? "";

            // Filter out unwanted artifacts (code blocks, explanations)
            completionText = CleanCompletionText(completionText);

            if (string.IsNullOrWhiteSpace(completionText))
                return null;

            return new CompletionResult
            {
                Text = completionText,
                CursorOffset = completionText.Length,
                Source = "inline-completion"
            };
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            // Log error but don't crash the editor
            Console.Error.WriteLine($"Completion error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Build a lightweight kernel with just the chat service, no plugins.
    /// </summary>
    private Kernel BuildLightweightKernel(ModelProviderConfig config)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddProviderChatCompletion(config);
        return builder.Build();
    }

    /// <summary>
    /// Get the provider configuration for completions.
    /// Prefer "fast" tier if available.
    /// </summary>
    private ModelProviderConfig GetProviderConfig()
    {
        // Use DeepSeek for fast completions if available
        if (_providerConfigs.TryGetValue(ModelProvider.DeepSeek, out var dsConfig))
            return dsConfig;

        return _providerConfigs[_defaultProvider];
    }

    /// <summary>
    /// Build the completion prompt with context.
    /// </summary>
    private static string BuildCompletionPrompt(CompletionRequest request)
    {
        var template = PromptTemplates.CompletionPrompt;

        return template
            .Replace("{filePath}", request.FilePath)
            .Replace("{language}", request.Language)
            .Replace("{line}", request.Line.ToString())
            .Replace("{column}", request.Column.ToString())
            .Replace("{codeBefore}", TruncateContext(request.CodeBefore, 500))
            .Replace("{codeAfter}", TruncateContext(request.CodeAfter, 200));
    }

    /// <summary>
    /// Truncate context to stay within token limits.
    /// </summary>
    private static string TruncateContext(string text, int maxLines)
    {
        var lines = text.Split('\n');
        if (lines.Length <= maxLines)
            return text;

        return string.Join('\n', lines.TakeLast(maxLines));
    }

    /// <summary>
    /// Clean completion text by removing code blocks and explanations.
    /// </summary>
    private static string CleanCompletionText(string text)
    {
        // Remove markdown code blocks
        text = System.Text.RegularExpressions.Regex.Replace(text, @"```[\w]*\n?", "");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"```", "");

        // Remove "Completion:" prefix if present
        if (text.StartsWith("Completion:", StringComparison.OrdinalIgnoreCase))
            text = text["Completion:".Length..].TrimStart();

        // Take only the first few lines (avoid explanations)
        var lines = text.Split('\n');
        if (lines.Length > 3)
            text = string.Join('\n', lines.Take(3));

        return text.Trim();
    }

    /// <summary>
    /// Generate a cache key based on the request context.
    /// </summary>
    private static string GenerateCacheKey(CompletionRequest request)
    {
        var sb = new StringBuilder();
        sb.Append(request.FilePath);
        sb.Append('|');
        sb.Append(request.Line);
        sb.Append('|');
        sb.Append(request.Column);
        sb.Append('|');

        // Hash the code context to keep key size manageable
        var contextHash = ComputeHash(request.CodeBefore + "|" + request.CodeAfter);
        sb.Append(contextHash);

        return sb.ToString();
    }

    /// <summary>
    /// Compute a short hash of a string for cache keys.
    /// </summary>
    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16]; // First 16 chars
    }

    /// <summary>
    /// Clear the completion cache.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        _pendingCancellation?.Cancel();
        _pendingCancellation?.Dispose();
        _debounceLock.Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>
/// Configuration for inline completion behavior.
/// </summary>
public class InlineCompletionConfig
{
    /// <summary>Temperature for completion generation (0.0 = deterministic).</summary>
    public double Temperature { get; set; } = 0.0;

    /// <summary>Maximum tokens to generate for a completion.</summary>
    public int MaxTokens { get; set; } = 200;

    /// <summary>Cache time-to-live.</summary>
    public TimeSpan CacheTTL { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Maximum number of cached completions.</summary>
    public int MaxCacheSize { get; set; } = 100;
}

/// <summary>
/// Request for inline code completion.
/// </summary>
public record CompletionRequest
{
    public required string FilePath { get; init; }
    public required string Language { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required string CodeBefore { get; init; }
    public required string CodeAfter { get; init; }
}

/// <summary>
/// Result of a completion request.
/// </summary>
public record CompletionResult
{
    public required string Text { get; init; }
    public required int CursorOffset { get; init; }
    public required string Source { get; init; }
}

/// <summary>
/// Cached completion with timestamp.
/// </summary>
internal record CachedCompletion(CompletionResult Result, DateTimeOffset Timestamp)
{
    public bool IsExpired(TimeSpan ttl) => DateTimeOffset.UtcNow - Timestamp > ttl;
}

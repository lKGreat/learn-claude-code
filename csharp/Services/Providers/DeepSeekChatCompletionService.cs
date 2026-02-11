using System.ClientModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;

namespace MiniClaudeCode.Services.Providers;

/// <summary>
/// DeepSeek chat completion service.
///
/// Implements IChatCompletionService by wrapping SK's OpenAIChatCompletionService
/// with the DeepSeek API endpoint. DeepSeek uses an OpenAI-compatible API format.
///
/// Endpoint: https://api.deepseek.com/v1
/// Models:   deepseek-chat (non-thinking), deepseek-reasoner (thinking)
/// Docs:     https://api-docs.deepseek.com/
/// </summary>
public class DeepSeekChatCompletionService : IChatCompletionService
{
    public const string DefaultEndpoint = "https://api.deepseek.com/v1";
    public const string DefaultModelId = "deepseek-chat";
    public const string ProviderName = "DeepSeek";

    private readonly IChatCompletionService _innerService;

    /// <summary>
    /// Create a DeepSeek chat completion service.
    /// </summary>
    /// <param name="apiKey">DeepSeek API key.</param>
    /// <param name="modelId">Model ID (default: deepseek-chat).</param>
    public DeepSeekChatCompletionService(string apiKey, string? modelId = null)
    {
        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions { Endpoint = new Uri(DefaultEndpoint) };
        var client = new OpenAIClient(credential, options);

        _innerService = new OpenAIChatCompletionService(
            modelId: modelId ?? DefaultModelId,
            openAIClient: client);
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Attributes => _innerService.Attributes;

    /// <inheritdoc/>
    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        return _innerService.GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        return _innerService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
    }
}

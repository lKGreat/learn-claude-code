using System.ClientModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;

namespace MiniClaudeCode.Services.Providers;

/// <summary>
/// Zhipu AI (智谱) GLM chat completion service.
///
/// Implements IChatCompletionService by wrapping SK's OpenAIChatCompletionService
/// with the Zhipu AI API endpoint. Zhipu uses an OpenAI-compatible API format.
///
/// Endpoint: https://open.bigmodel.cn/api/paas/v4
/// Models:   glm-4-plus, glm-4, glm-4-air, glm-4-flash, glm-5 (when available)
/// Docs:     https://open.bigmodel.cn/dev/api/normal-model/glm-4
/// </summary>
public class ZhipuChatCompletionService : IChatCompletionService
{
    public const string DefaultEndpoint = "https://open.bigmodel.cn/api/paas/v4";
    public const string DefaultModelId = "glm-4-plus";
    public const string ProviderName = "Zhipu";

    private readonly IChatCompletionService _innerService;

    /// <summary>
    /// Create a Zhipu GLM chat completion service.
    /// </summary>
    /// <param name="apiKey">Zhipu AI API key.</param>
    /// <param name="modelId">Model ID (default: glm-4-plus).</param>
    public ZhipuChatCompletionService(string apiKey, string? modelId = null)
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

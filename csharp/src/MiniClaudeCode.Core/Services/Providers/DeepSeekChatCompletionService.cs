using System.ClientModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;

namespace MiniClaudeCode.Core.Services.Providers;

/// <summary>
/// DeepSeek chat completion service using OpenAI-compatible API.
/// </summary>
public class DeepSeekChatCompletionService : IChatCompletionService
{
    public const string DefaultEndpoint = "https://api.deepseek.com/v1";
    public const string DefaultModelId = "deepseek-chat";

    private readonly IChatCompletionService _innerService;

    public DeepSeekChatCompletionService(string apiKey, string? modelId = null)
    {
        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions { Endpoint = new Uri(DefaultEndpoint) };
        var client = new OpenAIClient(credential, options);

        _innerService = new OpenAIChatCompletionService(
            modelId: modelId ?? DefaultModelId,
            openAIClient: client);
    }

    public IReadOnlyDictionary<string, object?> Attributes => _innerService.Attributes;

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null, CancellationToken cancellationToken = default)
        => _innerService.GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null, CancellationToken cancellationToken = default)
        => _innerService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
}

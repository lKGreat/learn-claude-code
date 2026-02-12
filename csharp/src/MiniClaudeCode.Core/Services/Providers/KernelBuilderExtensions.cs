using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MiniClaudeCode.Core.Configuration;

namespace MiniClaudeCode.Core.Services.Providers;

/// <summary>
/// Extension methods for IKernelBuilder to register custom model providers.
/// </summary>
public static class KernelBuilderExtensions
{
    public static IKernelBuilder AddDeepSeekChatCompletion(
        this IKernelBuilder builder, string apiKey, string? modelId = null, string? serviceId = null)
    {
        var service = new DeepSeekChatCompletionService(apiKey, modelId);
        builder.Services.AddKeyedSingleton<IChatCompletionService>(serviceId, service);
        return builder;
    }

    public static IKernelBuilder AddZhipuChatCompletion(
        this IKernelBuilder builder, string apiKey, string? modelId = null, string? serviceId = null)
    {
        var service = new ZhipuChatCompletionService(apiKey, modelId);
        builder.Services.AddKeyedSingleton<IChatCompletionService>(serviceId, service);
        return builder;
    }

    /// <summary>
    /// Register the correct chat completion service based on provider config.
    /// </summary>
    public static IKernelBuilder AddProviderChatCompletion(
        this IKernelBuilder builder, ModelProviderConfig config)
    {
        switch (config.Provider)
        {
            case ModelProvider.DeepSeek:
                builder.AddDeepSeekChatCompletion(config.ApiKey, config.ModelId);
                break;
            case ModelProvider.Zhipu:
                builder.AddZhipuChatCompletion(config.ApiKey, config.ModelId);
                break;
            case ModelProvider.OpenAI:
            default:
                if (!string.IsNullOrEmpty(config.BaseUrl))
                    builder.AddOpenAIChatCompletion(config.ModelId, new Uri(config.BaseUrl), config.ApiKey);
                else
                    builder.AddOpenAIChatCompletion(config.ModelId, config.ApiKey);
                break;
        }
        return builder;
    }
}

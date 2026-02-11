using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace MiniClaudeCode.Services.Providers;

/// <summary>
/// Extension methods for IKernelBuilder to register custom model providers.
///
/// Usage:
///   builder.AddDeepSeekChatCompletion(apiKey);
///   builder.AddZhipuChatCompletion(apiKey, modelId: "glm-4-plus");
///
/// These register the custom IChatCompletionService implementations
/// with optional serviceId for multi-provider scenarios.
/// </summary>
public static class KernelBuilderExtensions
{
    /// <summary>
    /// Add DeepSeek chat completion to the kernel builder.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="apiKey">DeepSeek API key.</param>
    /// <param name="modelId">Model ID (default: deepseek-chat).</param>
    /// <param name="serviceId">Optional service ID for keyed DI resolution.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IKernelBuilder AddDeepSeekChatCompletion(
        this IKernelBuilder builder,
        string apiKey,
        string? modelId = null,
        string? serviceId = null)
    {
        var service = new DeepSeekChatCompletionService(apiKey, modelId);
        builder.Services.AddKeyedSingleton<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>(
            serviceId, service);
        return builder;
    }

    /// <summary>
    /// Add Zhipu GLM chat completion to the kernel builder.
    /// </summary>
    /// <param name="builder">The kernel builder.</param>
    /// <param name="apiKey">Zhipu AI API key.</param>
    /// <param name="modelId">Model ID (default: glm-4-plus).</param>
    /// <param name="serviceId">Optional service ID for keyed DI resolution.</param>
    /// <returns>The kernel builder for chaining.</returns>
    public static IKernelBuilder AddZhipuChatCompletion(
        this IKernelBuilder builder,
        string apiKey,
        string? modelId = null,
        string? serviceId = null)
    {
        var service = new ZhipuChatCompletionService(apiKey, modelId);
        builder.Services.AddKeyedSingleton<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>(
            serviceId, service);
        return builder;
    }
}

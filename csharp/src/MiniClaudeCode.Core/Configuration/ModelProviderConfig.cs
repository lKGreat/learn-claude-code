namespace MiniClaudeCode.Core.Configuration;

/// <summary>
/// Supported model providers.
/// </summary>
public enum ModelProvider
{
    OpenAI,
    DeepSeek,
    Zhipu
}

/// <summary>
/// Configuration for a model provider.
/// Loaded from environment variables.
/// </summary>
public class ModelProviderConfig
{
    public ModelProvider Provider { get; set; }
    public string ApiKey { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string? BaseUrl { get; set; }

    public string DisplayName => Provider switch
    {
        ModelProvider.OpenAI => $"OpenAI ({ModelId})",
        ModelProvider.DeepSeek => $"DeepSeek ({ModelId})",
        ModelProvider.Zhipu => $"Zhipu GLM ({ModelId})",
        _ => Provider.ToString()
    };

    public static Dictionary<ModelProvider, ModelProviderConfig> LoadAll()
    {
        var configs = new Dictionary<ModelProvider, ModelProviderConfig>();

        var openaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openaiKey))
        {
            configs[ModelProvider.OpenAI] = new ModelProviderConfig
            {
                Provider = ModelProvider.OpenAI,
                ApiKey = openaiKey,
                ModelId = Environment.GetEnvironmentVariable("OPENAI_MODEL_ID") ?? "gpt-4o",
                BaseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL")
            };
        }

        var deepseekKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (!string.IsNullOrWhiteSpace(deepseekKey))
        {
            configs[ModelProvider.DeepSeek] = new ModelProviderConfig
            {
                Provider = ModelProvider.DeepSeek,
                ApiKey = deepseekKey,
                ModelId = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL_ID") ?? "deepseek-chat"
            };
        }

        var zhipuKey = Environment.GetEnvironmentVariable("ZHIPU_API_KEY");
        if (!string.IsNullOrWhiteSpace(zhipuKey))
        {
            configs[ModelProvider.Zhipu] = new ModelProviderConfig
            {
                Provider = ModelProvider.Zhipu,
                ApiKey = zhipuKey,
                ModelId = Environment.GetEnvironmentVariable("ZHIPU_MODEL_ID") ?? "glm-4-plus"
            };
        }

        return configs;
    }

    public static ModelProvider ResolveActiveProvider(Dictionary<ModelProvider, ModelProviderConfig> configs)
    {
        if (configs.Count == 0)
            throw new InvalidOperationException("No model providers configured.");

        var activeEnv = Environment.GetEnvironmentVariable("ACTIVE_PROVIDER")?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(activeEnv))
        {
            var parsed = ParseProviderName(activeEnv);
            if (parsed.HasValue && configs.ContainsKey(parsed.Value))
                return parsed.Value;
        }

        if (configs.Count == 1)
            return configs.Keys.First();

        // Default to first available
        return configs.Keys.First();
    }

    public static Dictionary<string, ModelProvider> LoadAgentProviderOverrides(
        Dictionary<ModelProvider, ModelProviderConfig> availableConfigs)
    {
        var overrides = new Dictionary<string, ModelProvider>();

        var mappings = new[]
        {
            ("explore", "AGENT_EXPLORE_PROVIDER"),
            ("code", "AGENT_CODE_PROVIDER"),
            ("plan", "AGENT_PLAN_PROVIDER"),
            ("generalPurpose", "AGENT_GENERAL_PROVIDER"),
        };

        foreach (var (agentType, envVar) in mappings)
        {
            var value = Environment.GetEnvironmentVariable(envVar)?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(value))
            {
                var parsed = ParseProviderName(value);
                if (parsed.HasValue && availableConfigs.ContainsKey(parsed.Value))
                    overrides[agentType] = parsed.Value;
            }
        }

        return overrides;
    }

    public static ModelProvider? ParseProviderName(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "openai" => ModelProvider.OpenAI,
            "deepseek" => ModelProvider.DeepSeek,
            "zhipu" or "glm" => ModelProvider.Zhipu,
            _ => null
        };
    }
}

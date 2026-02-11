namespace MiniClaudeCode.Services;

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
///
/// Loaded from environment variables:
///   ACTIVE_PROVIDER = openai | deepseek | zhipu
///   OPENAI_API_KEY, OPENAI_MODEL_ID, OPENAI_BASE_URL
///   DEEPSEEK_API_KEY, DEEPSEEK_MODEL_ID
///   ZHIPU_API_KEY, ZHIPU_MODEL_ID
///   AGENT_EXPLORE_PROVIDER, AGENT_CODE_PROVIDER, AGENT_PLAN_PROVIDER
/// </summary>
public class ModelProviderConfig
{
    public ModelProvider Provider { get; set; }
    public string ApiKey { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string DisplayName => Provider switch
    {
        ModelProvider.OpenAI => $"OpenAI ({ModelId})",
        ModelProvider.DeepSeek => $"DeepSeek ({ModelId})",
        ModelProvider.Zhipu => $"Zhipu GLM ({ModelId})",
        _ => Provider.ToString()
    };

    /// <summary>
    /// Load all available provider configurations from environment variables.
    /// Only returns providers that have an API key set.
    /// </summary>
    public static Dictionary<ModelProvider, ModelProviderConfig> LoadAll()
    {
        var configs = new Dictionary<ModelProvider, ModelProviderConfig>();

        // OpenAI
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

        // DeepSeek
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

        // Zhipu
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

    /// <summary>
    /// Resolve which provider to use as the active/primary provider.
    ///
    /// Priority:
    ///   1. ACTIVE_PROVIDER env var (if set and available)
    ///   2. Interactive selection (if multiple providers available)
    ///   3. The only available provider (if just one)
    /// </summary>
    public static ModelProvider ResolveActiveProvider(Dictionary<ModelProvider, ModelProviderConfig> configs)
    {
        if (configs.Count == 0)
            throw new InvalidOperationException("No model providers configured. Set at least one API key in .env file.");

        // 1. Check ACTIVE_PROVIDER env var
        var activeEnv = Environment.GetEnvironmentVariable("ACTIVE_PROVIDER")?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(activeEnv))
        {
            var parsed = ParseProviderName(activeEnv);
            if (parsed.HasValue && configs.ContainsKey(parsed.Value))
                return parsed.Value;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: ACTIVE_PROVIDER='{activeEnv}' is not available. Falling back to selection.");
            Console.ResetColor();
        }

        // 2. Single provider - use it directly
        if (configs.Count == 1)
            return configs.Keys.First();

        // 3. Interactive selection
        return InteractiveSelectProvider(configs);
    }

    /// <summary>
    /// Load per-agent-type provider overrides from environment variables.
    ///
    /// Env vars: AGENT_EXPLORE_PROVIDER, AGENT_CODE_PROVIDER, AGENT_PLAN_PROVIDER
    /// Returns a mapping of agent type name to provider. Missing entries = use default.
    /// </summary>
    public static Dictionary<string, ModelProvider> LoadAgentProviderOverrides(
        Dictionary<ModelProvider, ModelProviderConfig> availableConfigs)
    {
        var overrides = new Dictionary<string, ModelProvider>();

        var mappings = new[]
        {
            ("explore", "AGENT_EXPLORE_PROVIDER"),
            ("code", "AGENT_CODE_PROVIDER"),
            ("plan", "AGENT_PLAN_PROVIDER"),
        };

        foreach (var (agentType, envVar) in mappings)
        {
            var value = Environment.GetEnvironmentVariable(envVar)?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(value))
            {
                var parsed = ParseProviderName(value);
                if (parsed.HasValue && availableConfigs.ContainsKey(parsed.Value))
                {
                    overrides[agentType] = parsed.Value;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: {envVar}='{value}' is not available. Using default provider.");
                    Console.ResetColor();
                }
            }
        }

        return overrides;
    }

    /// <summary>
    /// Parse a provider name string to enum value.
    /// </summary>
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

    /// <summary>
    /// Interactive console prompt to select a provider.
    /// </summary>
    private static ModelProvider InteractiveSelectProvider(Dictionary<ModelProvider, ModelProviderConfig> configs)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\nAvailable model providers:");
        Console.ResetColor();

        var providers = configs.Values.ToList();
        for (int i = 0; i < providers.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] {providers[i].DisplayName}");
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"\nSelect provider (1-{providers.Count}): ");
        Console.ResetColor();

        while (true)
        {
            var input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= providers.Count)
            {
                var selected = providers[choice - 1];
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Using: {selected.DisplayName}");
                Console.ResetColor();
                return selected.Provider;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"Invalid choice. Enter 1-{providers.Count}: ");
            Console.ResetColor();
        }
    }
}

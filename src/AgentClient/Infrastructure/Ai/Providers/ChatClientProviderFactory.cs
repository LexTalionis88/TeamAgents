using AgentClient.Infrastructure.Ai.Abstractions;

namespace AgentClient.Infrastructure.Ai.Providers;

internal static class ChatClientProviderFactory
{
    private static readonly IReadOnlyDictionary<string, IChatClientProvider> Providers =
        new Dictionary<string, IChatClientProvider>(StringComparer.OrdinalIgnoreCase)
        {
            ["groq"] = new GroqChatClientProvider(),
            ["gemini"] = new GeminiChatClientProvider(),
            ["openrouter"] = new OpenRouterChatClientProvider(),
            ["ollama"] = new OllamaChatClientProvider(),
        };

    /// <summary>
    /// Выбирает адаптер по имени провайдера из конфигурации.
    /// </summary>
    /// <param name="modelProvider">Имя провайдера модели.</param>
    public static IChatClientProvider Resolve(string modelProvider)
    {
        var normalizedProvider = modelProvider.Trim().ToLowerInvariant();
        if (Providers.TryGetValue(normalizedProvider, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException(
            $"Неизвестный MODEL_PROVIDER='{modelProvider}'. Доступные провайдеры: {string.Join(", ", Providers.Keys)}.");
    }
}

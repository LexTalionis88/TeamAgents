using AgentClient.Configuration;
using AgentClient.Infrastructure.Ai.Abstractions;
using Microsoft.Extensions.AI;

namespace AgentClient.Infrastructure.Ai.Providers;

internal sealed class OpenRouterChatClientProvider : IChatClientProvider
{
    public string Name => "openrouter";

    public string CredentialConstraint =>
        "использовать OpenRouter только через OPENROUTER_API_KEY и никогда не раскрывать секрет";

    public bool RequiresLocalTypedJson => false;

    public int MaxAgentOutputTokens => 4096;

    public bool PreferCompactAgentInstructions => false;

    /// <summary>
    /// Возвращает описание выбранной модели OpenRouter для контекста задачи.
    /// </summary>
    /// <param name="options">Конфигурация AgentClient.</param>
    public string Describe(AgentClientOptions options) =>
        $"модель OpenRouter={options.OpenRouterModel}";

    /// <summary>
    /// Создаёт OpenRouter-клиент через OpenAI-совместимый endpoint.
    /// </summary>
    /// <param name="options">Конфигурация endpoint, модели и ключа.</param>
    public IChatClient CreateChatClient(AgentClientOptions options) =>
        OpenAiCompatibleChatClientFactory.Create(
            Name,
            "OPENROUTER_API_KEY",
            options.OpenRouterApiKey,
            options.OpenRouterBaseUrl,
            options.OpenRouterModel);
}

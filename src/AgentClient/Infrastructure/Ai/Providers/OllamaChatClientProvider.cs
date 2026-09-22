using AgentClient.Configuration;
using AgentClient.Infrastructure.Ai.Abstractions;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace AgentClient.Infrastructure.Ai.Providers;

internal sealed class OllamaChatClientProvider : IChatClientProvider
{
    public string Name => "ollama";

    public string CredentialConstraint =>
        "использовать локальную конечную точку Ollama и не применять облачные учётные данные";

    public bool RequiresLocalTypedJson => false;

    public int MaxAgentOutputTokens => 4096;

    public bool PreferCompactAgentInstructions => false;

    /// <summary>
    /// Возвращает описание локальной модели Ollama для контекста задачи.
    /// </summary>
    /// <param name="options">Конфигурация AgentClient.</param>
    public string Describe(AgentClientOptions options) =>
        $"локальная модель Ollama={options.OllamaModel}";

    /// <summary>
    /// Создаёт клиент локального Ollama endpoint.
    /// </summary>
    /// <param name="options">Конфигурация endpoint и модели.</param>
    public IChatClient CreateChatClient(AgentClientOptions options) =>
        new OllamaApiClient(new Uri(options.OllamaHost), options.OllamaModel);
}

using AgentClient.Configuration;
using AgentClient.Infrastructure.Ai.Abstractions;
using Microsoft.Extensions.AI;

namespace AgentClient.Infrastructure.Ai.Providers;

internal sealed class TuziChatClientProvider : IChatClientProvider
{
    public string Name => "tuzi";

    public string CredentialConstraint =>
        "использовать Tuzi только через TUZI_API_KEY и никогда не раскрывать секрет";

    public bool RequiresLocalTypedJson => false;

    public int MaxAgentOutputTokens => 4096;

    public bool PreferCompactAgentInstructions => false;

    /// <summary>
    /// Возвращает описание выбранной модели Tuzi для контекста задачи.
    /// </summary>
    /// <param name="options">Конфигурация AgentClient.</param>
    public string Describe(AgentClientOptions options) =>
        $"модель Tuzi={options.TuziModel}";

    /// <summary>
    /// Создаёт Tuzi-клиент через OpenAI-совместимый endpoint.
    /// </summary>
    /// <param name="options">Конфигурация endpoint, модели и ключа.</param>
    public IChatClient CreateChatClient(AgentClientOptions options) =>
        OpenAiCompatibleChatClientFactory.Create(
            Name,
            "TUZI_API_KEY",
            options.TuziApiKey,
            options.TuziBaseUrl,
            options.TuziModel);
}

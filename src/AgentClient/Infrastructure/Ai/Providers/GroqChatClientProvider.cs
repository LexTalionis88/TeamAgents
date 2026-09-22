using AgentClient.Configuration;
using AgentClient.Infrastructure.Ai.Abstractions;
using Microsoft.Extensions.AI;

namespace AgentClient.Infrastructure.Ai.Providers;

internal sealed class GroqChatClientProvider : IChatClientProvider
{
    public string Name => "groq";

    public string CredentialConstraint =>
        "использовать Groq только через GROQ_API_KEY и никогда не раскрывать секрет";

    public bool RequiresLocalTypedJson => true;

    // Бесплатная квота Groq для доступных моделей может ограничивать вывод 1000 токенами.
    // Оставляем запас, чтобы запросы не отклонялись из-за служебных токенов провайдера.
    public int MaxAgentOutputTokens => 900;

    public bool PreferCompactAgentInstructions => true;

    /// <summary>
    /// Возвращает описание выбранной модели Groq для контекста задачи.
    /// </summary>
    /// <param name="options">Конфигурация AgentClient.</param>
    public string Describe(AgentClientOptions options) =>
        $"модель Groq={options.GroqModel}";

    /// <summary>
    /// Создаёт Groq-клиент через OpenAI-совместимый endpoint без скрытых повторов.
    /// </summary>
    /// <param name="options">Конфигурация endpoint, модели и ключа.</param>
    public IChatClient CreateChatClient(AgentClientOptions options) =>
        OpenAiCompatibleChatClientFactory.Create(
            Name,
            "GROQ_API_KEY",
            options.GroqApiKey,
            options.GroqBaseUrl,
            options.GroqModel,
            retryCount: 0);
}

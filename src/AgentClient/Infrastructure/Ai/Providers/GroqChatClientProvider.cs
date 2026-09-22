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

    public string Describe(AgentClientOptions options) =>
        $"модель Groq={options.GroqModel}";

    public IChatClient CreateChatClient(AgentClientOptions options) =>
        OpenAiCompatibleChatClientFactory.Create(
            Name,
            "GROQ_API_KEY",
            options.GroqApiKey,
            options.GroqBaseUrl,
            options.GroqModel,
            retryCount: 0);
}

using AgentClient.Configuration;
using AgentClient.Infrastructure.Ai.Abstractions;
using Microsoft.Extensions.AI;

namespace AgentClient.Infrastructure.Ai.Providers;

internal sealed class GeminiChatClientProvider : IChatClientProvider
{
    public string Name => "gemini";

    public string CredentialConstraint =>
        "использовать Gemini только через GEMINI_API_KEY и никогда не раскрывать секрет";

    public bool RequiresLocalTypedJson => true;

    public int MaxAgentOutputTokens => 4096;

    public bool PreferCompactAgentInstructions => false;

    /// <summary>
    /// Возвращает описание выбранной модели Gemini для контекста задачи.
    /// </summary>
    /// <param name="options">Конфигурация AgentClient.</param>
    public string Describe(AgentClientOptions options) =>
        $"модель Gemini={options.GeminiModel}";

    /// <summary>
    /// Создаёт Gemini-клиент через OpenAI-совместимый endpoint.
    /// </summary>
    /// <param name="options">Конфигурация endpoint, модели и ключа.</param>
    public IChatClient CreateChatClient(AgentClientOptions options)
    {
        var client = OpenAiCompatibleChatClientFactory.Create(
            Name,
            "GEMINI_API_KEY",
            options.GeminiApiKey,
            options.GeminiBaseUrl,
            options.GeminiModel);
        return new GeminiCompatibleChatClient(client);
    }
}

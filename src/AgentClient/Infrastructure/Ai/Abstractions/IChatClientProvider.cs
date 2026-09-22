using AgentClient.Configuration;
using Microsoft.Extensions.AI;

namespace AgentClient.Infrastructure.Ai.Abstractions;

internal interface IChatClientProvider
{
    string Name { get; }

    string CredentialConstraint { get; }

    bool RequiresLocalTypedJson { get; }

    /// <summary>
    /// Безопасный максимальный размер ответа одного агентского вызова для провайдера.
    /// </summary>
    int MaxAgentOutputTokens { get; }

    /// <summary>
    /// Признак того, что провайдеру нужен сокращённый профиль инструкций агентов.
    /// </summary>
    bool PreferCompactAgentInstructions { get; }

    string Describe(AgentClientOptions options);

    IChatClient CreateChatClient(AgentClientOptions options);
}

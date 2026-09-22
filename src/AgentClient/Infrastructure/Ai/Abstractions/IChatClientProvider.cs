using AgentClient.Configuration;
using Microsoft.Extensions.AI;

namespace AgentClient.Infrastructure.Ai.Abstractions;

/// <summary>
/// Описывает адаптер конкретного поставщика chat-модели.
/// </summary>
internal interface IChatClientProvider
{
    /// <summary>
    /// Возвращает стабильное имя поставщика для выбора из конфигурации.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Возвращает ограничение на использование учётных данных поставщика.
    /// </summary>
    string CredentialConstraint { get; }

    /// <summary>
    /// Показывает, нужна ли локальная проверка typed JSON для поставщика.
    /// </summary>
    bool RequiresLocalTypedJson { get; }

    /// <summary>
    /// Безопасный максимальный размер ответа одного агентского вызова для провайдера.
    /// </summary>
    int MaxAgentOutputTokens { get; }

    /// <summary>
    /// Признак того, что провайдеру нужен сокращённый профиль инструкций агентов.
    /// </summary>
    bool PreferCompactAgentInstructions { get; }

    /// <summary>
    /// Возвращает русское описание provider-specific конфигурации для задачи.
    /// </summary>
    /// <param name="options">Общая конфигурация AgentClient.</param>
    string Describe(AgentClientOptions options);

    /// <summary>
    /// Создаёт chat client конкретного провайдера.
    /// </summary>
    /// <param name="options">Конфигурация endpoint, модели и учетных данных.</param>
    IChatClient CreateChatClient(AgentClientOptions options);
}

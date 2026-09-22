using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;

namespace AgentClient.Infrastructure.Ai.Providers;

internal static class OpenAiCompatibleChatClientFactory
{
    /// <summary>
    /// Создаёт IChatClient для OpenAI-совместимого endpoint.
    /// </summary>
    /// <param name="providerName">Имя провайдера для сообщений об ошибках.</param>
    /// <param name="credentialVariable">Имя переменной с API-ключом.</param>
    /// <param name="apiKey">API-ключ из окружения.</param>
    /// <param name="endpoint">Адрес OpenAI-совместимого endpoint.</param>
    /// <param name="model">Имя модели.</param>
    /// <param name="retryCount">Количество повторов HTTP-клиента.</param>
    public static IChatClient Create(
        string providerName,
        string credentialVariable,
        string? apiKey,
        string endpoint,
        string model,
        int retryCount = 3)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"MODEL_PROVIDER={providerName} требует {credentialVariable}; секреты читаются только из переменных окружения.");
        }

        var credential = new System.ClientModel.ApiKeyCredential(apiKey);
        var client = new OpenAI.OpenAIClient(
            credential,
            new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint),
                RetryPolicy = new ClientRetryPolicy(retryCount),
            });
        return client.GetChatClient(model).AsIChatClient();
    }
}

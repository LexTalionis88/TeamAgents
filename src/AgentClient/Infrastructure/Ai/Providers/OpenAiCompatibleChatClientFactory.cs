using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;

namespace AgentClient.Infrastructure.Ai.Providers;

internal static class OpenAiCompatibleChatClientFactory
{
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

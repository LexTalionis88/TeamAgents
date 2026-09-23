using AgentClient.Configuration;
using AgentClient.Infrastructure.Ai.Abstractions;
using Microsoft.Extensions.AI;

namespace AgentClient.Infrastructure.Ai.Providers;

internal sealed class CloudflareChatClientProvider : IChatClientProvider
{
    public string Name => "cloudflare";

    public string CredentialConstraint =>
        "использовать Cloudflare Workers AI только через CLOUDFLARE_API_TOKEN и CLOUDFLARE_ACCOUNT_ID, никогда не раскрывать секрет";

    // Cloudflare JSON Mode не гарантирует соответствие JSON Schema, поэтому
    // typed-ответы проверяются локально, а tool calling остаётся совместимым.
    public bool RequiresLocalTypedJson => true;

    public int MaxAgentOutputTokens => 2048;

    public bool PreferCompactAgentInstructions => true;

    /// <summary>
    /// Возвращает описание выбранной модели Cloudflare Workers AI.
    /// </summary>
    /// <param name="options">Конфигурация AgentClient.</param>
    public string Describe(AgentClientOptions options) =>
        $"модель Cloudflare Workers AI={options.CloudflareModel}";

    /// <summary>
    /// Создаёт Cloudflare-клиент через OpenAI-совместимый Workers AI endpoint.
    /// </summary>
    /// <param name="options">Конфигурация endpoint, модели и токена.</param>
    public IChatClient CreateChatClient(AgentClientOptions options)
    {
        var endpoint = ResolveEndpoint(options);
        var client = OpenAiCompatibleChatClientFactory.Create(
            Name,
            "CLOUDFLARE_API_TOKEN",
            options.CloudflareApiToken,
            endpoint,
            options.CloudflareModel);

        return new CloudflareCompatibleChatClient(client);
    }

    private static string ResolveEndpoint(AgentClientOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CloudflareBaseUrl))
        {
            return options.CloudflareBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(options.CloudflareAccountId))
        {
            throw new InvalidOperationException(
                "MODEL_PROVIDER=cloudflare требует CLOUDFLARE_ACCOUNT_ID или CLOUDFLARE_BASE_URL.");
        }

        return $"https://api.cloudflare.com/client/v4/accounts/" +
            $"{Uri.EscapeDataString(options.CloudflareAccountId)}/ai/v1";
    }
}

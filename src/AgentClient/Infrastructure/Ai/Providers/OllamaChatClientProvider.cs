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

    public string Describe(AgentClientOptions options) =>
        $"локальная модель Ollama={options.OllamaModel}";

    public IChatClient CreateChatClient(AgentClientOptions options) =>
        new OllamaApiClient(new Uri(options.OllamaHost), options.OllamaModel);
}

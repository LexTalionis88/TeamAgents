using Microsoft.Extensions.AI;

namespace AgentClient.Infrastructure.Ai.Providers;

/// <summary>
/// Адаптирует запросы Gemini через OpenAI-совместимый интерфейс к более узкой
/// схеме Gemini. Typed-ответы обрабатываются через TypedAgentRunner.
/// </summary>
internal sealed class GeminiCompatibleChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(messages, NormalizeOptions(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(messages, NormalizeOptions(options), cancellationToken);

    private static ChatOptions? NormalizeOptions(ChatOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        var copy = options.Clone();
        copy.ResponseFormat = null;
        var additionalProperties = copy.AdditionalProperties is null
            ? new Dictionary<string, object?>()
            : copy.AdditionalProperties.ToDictionary(pair => pair.Key, pair => pair.Value);
        additionalProperties["strict"] = false;
        copy.AdditionalProperties = new AdditionalPropertiesDictionary(additionalProperties);
        return copy;
    }
}

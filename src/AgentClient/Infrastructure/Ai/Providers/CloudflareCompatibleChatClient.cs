using Microsoft.Extensions.AI;

namespace AgentClient.Infrastructure.Ai.Providers;

/// <summary>
/// Нормализует assistant-сообщения с function call для Cloudflare Workers AI.
///
/// Cloudflare Chat Completions отклоняет продолжение tool-call диалога, если
/// content assistant-сообщения сериализуется как null. Пустой TextContent
/// заставляет OpenAI-совместимый адаптер отправить content как пустую строку.
/// </summary>
internal sealed class CloudflareCompatibleChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(NormalizeMessages(messages), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(NormalizeMessages(messages), options, cancellationToken);

    private static IEnumerable<ChatMessage> NormalizeMessages(IEnumerable<ChatMessage> messages)
    {
        foreach (var message in messages)
        {
            if (message.Role != ChatRole.Assistant ||
                !message.Contents.Any(content => content is FunctionCallContent) ||
                message.Contents.Any(content => content is TextContent))
            {
                yield return message;
                continue;
            }

            var normalized = message.Clone();
            var contents = new List<AIContent>(message.Contents.Count + 1)
            {
                new TextContent(string.Empty),
            };
            contents.AddRange(message.Contents);
            normalized.Contents = contents;
            yield return normalized;
        }
    }
}

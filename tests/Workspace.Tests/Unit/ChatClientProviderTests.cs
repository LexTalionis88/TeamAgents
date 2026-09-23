using AgentClient.Configuration;
using AgentClient.Infrastructure.Ai.Providers;
using Microsoft.Extensions.AI;

namespace Workspace.Tests.Unit;

[TestFixture]
public sealed class ChatClientProviderTests
{
    /// <summary>
    /// Проверяет выбор адаптера для каждого поддерживаемого provider.
    /// </summary>
    [TestCase("groq", typeof(GroqChatClientProvider))]
    [TestCase("gemini", typeof(GeminiChatClientProvider))]
    [TestCase("openrouter", typeof(OpenRouterChatClientProvider))]
    [TestCase("ollama", typeof(OllamaChatClientProvider))]
    [TestCase("tuzi", typeof(TuziChatClientProvider))]
    [TestCase("cloudflare", typeof(CloudflareChatClientProvider))]
    public void ResolveSelectsProviderAdapter(string name, Type expectedType)
    {
        var provider = ChatClientProviderFactory.Resolve(name);

        Assert.That(provider, Is.TypeOf(expectedType));
        Assert.That(provider.Name, Is.EqualTo(name));
    }

    /// <summary>
    /// Проверяет регистронезависимый выбор provider.
    /// </summary>
    [Test]
    public void ResolveIsCaseInsensitive()
    {
        var provider = ChatClientProviderFactory.Resolve("GrOq");

        Assert.That(provider, Is.TypeOf<GroqChatClientProvider>());
    }

    /// <summary>
    /// Проверяет отклонение неизвестного provider.
    /// </summary>
    [Test]
    public void ResolveRejectsUnknownProvider()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ChatClientProviderFactory.Resolve("unknown"));

        Assert.That(exception!.Message, Does.Contain("MODEL_PROVIDER='unknown'"));
    }

    /// <summary>
    /// Проверяет capability Groq для компактных инструкций и бюджета ответа.
    /// </summary>
    [Test]
    public void GroqProviderDeclaresCompactPromptAndOutputBudget()
    {
        var provider = ChatClientProviderFactory.Resolve("groq");

        Assert.Multiple(() =>
        {
            Assert.That(provider.MaxAgentOutputTokens, Is.EqualTo(900));
            Assert.That(provider.PreferCompactAgentInstructions, Is.True);
        });
    }

    /// <summary>
    /// Проверяет capability Cloudflare для tool calling и локальной typed-валидации.
    /// </summary>
    [Test]
    public void CloudflareProviderDeclaresAgentCapabilities()
    {
        var provider = ChatClientProviderFactory.Resolve("cloudflare");

        Assert.Multiple(() =>
        {
            Assert.That(provider.RequiresLocalTypedJson, Is.True);
            Assert.That(provider.PreferCompactAgentInstructions, Is.True);
            Assert.That(provider.MaxAgentOutputTokens, Is.EqualTo(2048));
        });
    }

    /// <summary>
    /// Проверяет понятную ошибку при отсутствии Cloudflare account id и endpoint.
    /// </summary>
    [Test]
    public void CloudflareProviderRequiresAccountOrCustomEndpoint()
    {
        var provider = ChatClientProviderFactory.Resolve("cloudflare");
        var options = AgentClientOptions.FromEnvironment() with
        {
            CloudflareApiToken = "test-token",
            CloudflareAccountId = null,
            CloudflareBaseUrl = null,
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.CreateChatClient(options));

        Assert.That(exception!.Message, Does.Contain("CLOUDFLARE_ACCOUNT_ID"));
    }

    /// <summary>
    /// Проверяет нормализацию assistant function call для Cloudflare Chat Completions.
    /// </summary>
    [Test]
    public async Task CloudflareClientAddsEmptyTextToAssistantFunctionCall()
    {
        var innerClient = new RecordingChatClient();
        using var client = new CloudflareCompatibleChatClient(innerClient);
        var functionCall = new FunctionCallContent(
            "call-1",
            "get_workspace_status",
            new Dictionary<string, object?>());
        var assistantMessage = new ChatMessage(
            ChatRole.Assistant,
            new List<AIContent> { functionCall });

        await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User, "Check status."),
            assistantMessage,
            new ChatMessage(
                ChatRole.Tool,
                new List<AIContent>
                {
                    new FunctionResultContent("call-1", "ok"),
                }),
        ]);

        var normalizedAssistant = innerClient.Messages.Single(message => message.Role == ChatRole.Assistant);

        Assert.Multiple(() =>
        {
            Assert.That(normalizedAssistant.Contents.OfType<TextContent>().Single().Text, Is.Empty);
            Assert.That(normalizedAssistant.Contents.OfType<FunctionCallContent>().Single().CallId, Is.EqualTo("call-1"));
            Assert.That(assistantMessage.Contents.OfType<TextContent>(), Is.Empty);
        });
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public List<ChatMessage> Messages { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Messages.AddRange(messages);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Messages.AddRange(messages);
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}

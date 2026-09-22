using AgentClient.Infrastructure.Ai.Providers;

namespace Workspace.Tests.Unit;

[TestFixture]
public sealed class ChatClientProviderTests
{
    [TestCase("groq", typeof(GroqChatClientProvider))]
    [TestCase("gemini", typeof(GeminiChatClientProvider))]
    [TestCase("openrouter", typeof(OpenRouterChatClientProvider))]
    [TestCase("ollama", typeof(OllamaChatClientProvider))]
    public void ResolveSelectsProviderAdapter(string name, Type expectedType)
    {
        var provider = ChatClientProviderFactory.Resolve(name);

        Assert.That(provider, Is.TypeOf(expectedType));
        Assert.That(provider.Name, Is.EqualTo(name));
    }

    [Test]
    public void ResolveIsCaseInsensitive()
    {
        var provider = ChatClientProviderFactory.Resolve("GrOq");

        Assert.That(provider, Is.TypeOf<GroqChatClientProvider>());
    }

    [Test]
    public void ResolveRejectsUnknownProvider()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ChatClientProviderFactory.Resolve("unknown"));

        Assert.That(exception!.Message, Does.Contain("MODEL_PROVIDER='unknown'"));
    }

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
}

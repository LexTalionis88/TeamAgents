using AgentClient.Configuration;

namespace Workspace.Tests.Unit;

[TestFixture]
[NonParallelizable]
public sealed class AgentClientOptionsTests
{
    [TestCase("-1", 0)]
    [TestCase("0", 0)]
    [TestCase("2", 2)]
    [TestCase("99", 3)]
    public void FromEnvironment_ClampsMaxCycles(string value, int expected)
    {
        var previous = Environment.GetEnvironmentVariable("WORKFLOW_MAX_CYCLES");
        try
        {
            Environment.SetEnvironmentVariable("WORKFLOW_MAX_CYCLES", value);

            var options = AgentClientOptions.FromEnvironment();

            Assert.That(options.MaxCycles, Is.EqualTo(expected));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WORKFLOW_MAX_CYCLES", previous);
        }
    }

    [Test]
    public void FromEnvironment_UsesSafeDefaults()
    {
        var names = new[] { "MODEL_PROVIDER", "OLLAMA_HOST", "OLLAMA_MODEL", "OPENROUTER_MODEL", "OPENROUTER_BASE_URL", "OPENROUTER_API_KEY", "GEMINI_MODEL", "GEMINI_BASE_URL", "GEMINI_API_KEY", "GROQ_MODEL", "GROQ_BASE_URL", "GROQ_API_KEY", "WORKFLOW_FEATURE", "WORKFLOW_MAX_CYCLES", "WORKFLOW_MAX_WORK_ITEMS", "WORKFLOW_AGENT_TIMEOUT_SECONDS" };
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in names)
            {
                Environment.SetEnvironmentVariable(name, null);
            }

            var options = AgentClientOptions.FromEnvironment();

            Assert.Multiple(() =>
            {
                Assert.That(options.ModelProvider, Is.EqualTo("ollama"));
                Assert.That(options.OllamaHost, Is.EqualTo("http://localhost:11434"));
                Assert.That(options.OllamaModel, Is.EqualTo("qwen3:1.7b"));
                Assert.That(options.OpenRouterModel, Is.EqualTo("openrouter/free"));
                Assert.That(options.OpenRouterBaseUrl, Is.EqualTo("https://openrouter.ai/api/v1"));
                Assert.That(options.OpenRouterApiKey, Is.Null);
                Assert.That(options.GeminiModel, Is.EqualTo("gemini-3.8-flash"));
                Assert.That(options.GeminiBaseUrl, Is.EqualTo("https://generativelanguage.googleapis.com/v1beta/openai/"));
                Assert.That(options.GeminiApiKey, Is.Null);
                Assert.That(options.GroqModel, Is.EqualTo("openai/gpt-oss-120b"));
                Assert.That(options.GroqBaseUrl, Is.EqualTo("https://api.groq.com/openai/v1"));
                Assert.That(options.GroqApiKey, Is.Null);
                Assert.That(options.WorkflowFeature, Is.EqualTo("workspace-architecture-review"));
                Assert.That(options.MaxCycles, Is.EqualTo(2));
                Assert.That(options.MaxWorkItems, Is.EqualTo(6));
                Assert.That(options.AgentTimeoutSeconds, Is.EqualTo(180));
            });
        }
        finally
        {
            foreach (var pair in previous)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }

    [Test]
    public void FromEnvironment_ReadsGeminiConfiguration()
    {
        var names = new[] { "MODEL_PROVIDER", "GEMINI_MODEL", "GEMINI_BASE_URL", "GEMINI_API_KEY" };
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("MODEL_PROVIDER", "gemini");
            Environment.SetEnvironmentVariable("GEMINI_MODEL", "gemini-test");
            Environment.SetEnvironmentVariable("GEMINI_BASE_URL", "https://example.test/openai/");
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", "test-key");

            var options = AgentClientOptions.FromEnvironment();

            Assert.Multiple(() =>
            {
                Assert.That(options.ModelProvider, Is.EqualTo("gemini"));
                Assert.That(options.GeminiModel, Is.EqualTo("gemini-test"));
                Assert.That(options.GeminiBaseUrl, Is.EqualTo("https://example.test/openai/"));
                Assert.That(options.GeminiApiKey, Is.EqualTo("test-key"));
            });
        }
        finally
        {
            foreach (var pair in previous)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }

    [Test]
    public void FromEnvironment_ReadsGroqConfiguration()
    {
        var names = new[] { "MODEL_PROVIDER", "GROQ_MODEL", "GROQ_BASE_URL", "GROQ_API_KEY" };
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("MODEL_PROVIDER", "groq");
            Environment.SetEnvironmentVariable("GROQ_MODEL", "openai/gpt-oss-test");
            Environment.SetEnvironmentVariable("GROQ_BASE_URL", "https://example.test/openai/v1");
            Environment.SetEnvironmentVariable("GROQ_API_KEY", "test-key");

            var options = AgentClientOptions.FromEnvironment();

            Assert.Multiple(() =>
            {
                Assert.That(options.ModelProvider, Is.EqualTo("groq"));
                Assert.That(options.GroqModel, Is.EqualTo("openai/gpt-oss-test"));
                Assert.That(options.GroqBaseUrl, Is.EqualTo("https://example.test/openai/v1"));
                Assert.That(options.GroqApiKey, Is.EqualTo("test-key"));
            });
        }
        finally
        {
            foreach (var pair in previous)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }

    [TestCase("0", 1)]
    [TestCase("3", 3)]
    [TestCase("99", 6)]
    public void FromEnvironment_ClampsMaxWorkItems(string value, int expected)
    {
        var previous = Environment.GetEnvironmentVariable("WORKFLOW_MAX_WORK_ITEMS");
        try
        {
            Environment.SetEnvironmentVariable("WORKFLOW_MAX_WORK_ITEMS", value);

            var options = AgentClientOptions.FromEnvironment();

            Assert.That(options.MaxWorkItems, Is.EqualTo(expected));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WORKFLOW_MAX_WORK_ITEMS", previous);
        }
    }

    [TestCase("1", 30)]
    [TestCase("180", 180)]
    [TestCase("999", 600)]
    public void FromEnvironment_ClampsAgentTimeout(string value, int expected)
    {
        var previous = Environment.GetEnvironmentVariable("WORKFLOW_AGENT_TIMEOUT_SECONDS");
        try
        {
            Environment.SetEnvironmentVariable("WORKFLOW_AGENT_TIMEOUT_SECONDS", value);

            var options = AgentClientOptions.FromEnvironment();

            Assert.That(options.AgentTimeoutSeconds, Is.EqualTo(expected));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WORKFLOW_AGENT_TIMEOUT_SECONDS", previous);
        }
    }
}

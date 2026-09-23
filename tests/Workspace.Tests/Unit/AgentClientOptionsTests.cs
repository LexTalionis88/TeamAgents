using AgentClient.Configuration;

namespace Workspace.Tests.Unit;

[TestFixture]
[NonParallelizable]
public sealed class AgentClientOptionsTests
{
    /// <summary>
    /// Проверяет ограничение количества workflow-циклов допустимым диапазоном.
    /// </summary>
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

    /// <summary>
    /// Проверяет безопасные значения конфигурации по умолчанию.
    /// </summary>
    [Test]
    public void FromEnvironment_UsesSafeDefaults()
    {
        var names = new[] { "MODEL_PROVIDER", "OLLAMA_HOST", "OLLAMA_MODEL", "OPENROUTER_MODEL", "OPENROUTER_BASE_URL", "OPENROUTER_API_KEY", "GEMINI_MODEL", "GEMINI_BASE_URL", "GEMINI_API_KEY", "GROQ_MODEL", "GROQ_BASE_URL", "GROQ_API_KEY", "TUZI_MODEL", "TUZI_BASE_URL", "TUZI_API_KEY", "CLOUDFLARE_MODEL", "CLOUDFLARE_BASE_URL", "CLOUDFLARE_ACCOUNT_ID", "CLOUDFLARE_API_TOKEN", "WORKFLOW_FEATURE", "WORKFLOW_MAX_CYCLES", "WORKFLOW_MAX_WORK_ITEMS", "WORKFLOW_AGENT_TIMEOUT_SECONDS", "WORKFLOW_TIMEOUT_SECONDS", "WORKFLOW_READ_ONLY" };
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
                Assert.That(options.TuziModel, Is.EqualTo("gpt-4.1-mini"));
                Assert.That(options.TuziBaseUrl, Is.EqualTo("https://api.tu-zi.com/v1"));
                Assert.That(options.TuziApiKey, Is.Null);
                Assert.That(options.CloudflareModel, Is.EqualTo("@cf/ibm-granite/granite-4.0-h-micro"));
                Assert.That(options.CloudflareBaseUrl, Is.Null);
                Assert.That(options.CloudflareAccountId, Is.Null);
                Assert.That(options.CloudflareApiToken, Is.Null);
                Assert.That(options.WorkflowFeature, Is.EqualTo("workspace-architecture-review"));
                Assert.That(options.MaxCycles, Is.EqualTo(2));
                Assert.That(options.MaxWorkItems, Is.EqualTo(6));
                Assert.That(options.AgentTimeoutSeconds, Is.EqualTo(180));
                Assert.That(options.WorkflowTimeoutSeconds, Is.EqualTo(900));
                Assert.That(options.WorkflowReadOnly, Is.False);
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

    /// <summary>
    /// Проверяет чтение конфигурации Gemini из окружения.
    /// </summary>
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

    /// <summary>
    /// Проверяет чтение конфигурации Groq из окружения.
    /// </summary>
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

    /// <summary>
    /// Проверяет чтение конфигурации Tuzi из окружения.
    /// </summary>
    [Test]
    public void FromEnvironment_ReadsTuziConfiguration()
    {
        var names = new[] { "MODEL_PROVIDER", "TUZI_MODEL", "TUZI_BASE_URL", "TUZI_API_KEY" };
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("MODEL_PROVIDER", "tuzi");
            Environment.SetEnvironmentVariable("TUZI_MODEL", "tuzi-test-model");
            Environment.SetEnvironmentVariable("TUZI_BASE_URL", "https://example.test/v1");
            Environment.SetEnvironmentVariable("TUZI_API_KEY", "test-key");

            var options = AgentClientOptions.FromEnvironment();

            Assert.Multiple(() =>
            {
                Assert.That(options.ModelProvider, Is.EqualTo("tuzi"));
                Assert.That(options.TuziModel, Is.EqualTo("tuzi-test-model"));
                Assert.That(options.TuziBaseUrl, Is.EqualTo("https://example.test/v1"));
                Assert.That(options.TuziApiKey, Is.EqualTo("test-key"));
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

    /// <summary>
    /// Проверяет чтение Cloudflare Workers AI конфигурации из окружения.
    /// </summary>
    [Test]
    public void FromEnvironment_ReadsCloudflareConfiguration()
    {
        var names = new[] { "MODEL_PROVIDER", "CLOUDFLARE_MODEL", "CLOUDFLARE_BASE_URL", "CLOUDFLARE_ACCOUNT_ID", "CLOUDFLARE_API_TOKEN" };
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("MODEL_PROVIDER", "cloudflare");
            Environment.SetEnvironmentVariable("CLOUDFLARE_MODEL", "@cf/test/model");
            Environment.SetEnvironmentVariable("CLOUDFLARE_BASE_URL", "https://example.test/accounts/test/ai/v1");
            Environment.SetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID", "account-test");
            Environment.SetEnvironmentVariable("CLOUDFLARE_API_TOKEN", "test-token");

            var options = AgentClientOptions.FromEnvironment();

            Assert.Multiple(() =>
            {
                Assert.That(options.ModelProvider, Is.EqualTo("cloudflare"));
                Assert.That(options.CloudflareModel, Is.EqualTo("@cf/test/model"));
                Assert.That(options.CloudflareBaseUrl, Is.EqualTo("https://example.test/accounts/test/ai/v1"));
                Assert.That(options.CloudflareAccountId, Is.EqualTo("account-test"));
                Assert.That(options.CloudflareApiToken, Is.EqualTo("test-token"));
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

    /// <summary>
    /// Проверяет ограничение количества WorkItem допустимым диапазоном.
    /// </summary>
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

    /// <summary>
    /// Проверяет ограничение тайм-аута агентского вызова допустимым диапазоном.
    /// </summary>
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

    /// <summary>
    /// Проверяет ограничение общего deadline workflow.
    /// </summary>
    [TestCase("1", 60)]
    [TestCase("900", 900)]
    [TestCase("9999", 3600)]
    public void FromEnvironment_ClampsWorkflowTimeout(string value, int expected)
    {
        var previous = Environment.GetEnvironmentVariable("WORKFLOW_TIMEOUT_SECONDS");
        try
        {
            Environment.SetEnvironmentVariable("WORKFLOW_TIMEOUT_SECONDS", value);

            var options = AgentClientOptions.FromEnvironment();

            Assert.That(options.WorkflowTimeoutSeconds, Is.EqualTo(expected));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WORKFLOW_TIMEOUT_SECONDS", previous);
        }
    }

    /// <summary>
    /// Проверяет включение безопасного read-only режима workflow.
    /// </summary>
    [TestCase("true", true)]
    [TestCase("TRUE", true)]
    [TestCase("false", false)]
    [TestCase("1", false)]
    public void FromEnvironment_ReadsWorkflowReadOnly(string value, bool expected)
    {
        var previous = Environment.GetEnvironmentVariable("WORKFLOW_READ_ONLY");
        try
        {
            Environment.SetEnvironmentVariable("WORKFLOW_READ_ONLY", value);

            var options = AgentClientOptions.FromEnvironment();

            Assert.That(options.WorkflowReadOnly, Is.EqualTo(expected));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WORKFLOW_READ_ONLY", previous);
        }
    }
}

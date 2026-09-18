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
        var names = new[] { "OLLAMA_HOST", "OLLAMA_MODEL", "WORKFLOW_FEATURE", "WORKFLOW_MAX_CYCLES" };
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
                Assert.That(options.OllamaHost, Is.EqualTo("http://localhost:11434"));
                Assert.That(options.OllamaModel, Is.EqualTo("qwen3:1.7b"));
                Assert.That(options.WorkflowFeature, Is.EqualTo("workspace-architecture-review"));
                Assert.That(options.MaxCycles, Is.EqualTo(2));
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
}

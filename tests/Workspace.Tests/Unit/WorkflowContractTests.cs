using System.Text.Json;
using AgentClient;

namespace Workspace.Tests.Unit;

[TestFixture]
public sealed class WorkflowContractTests
{
    [Test]
    public void ArchitectureQuestion_RoundTripsAsJson()
    {
        var contract = new ArchitectureQuestion(
            "Добавить MCP tool",
            "workspace",
            ["не менять stdio"]);

        var json = JsonSerializer.Serialize(contract);
        var restored = JsonSerializer.Deserialize<ArchitectureQuestion>(json);

        Assert.That(restored, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(restored!.Question, Is.EqualTo(contract.Question));
            Assert.That(restored.Context, Is.EqualTo(contract.Context));
            Assert.That(restored.Constraints, Is.EqualTo(contract.Constraints));
        });
    }

    [Test]
    public void EscalationInput_DefaultsToExpectedStage()
    {
        var input = new TestEscalationInput(
            new ImplementationResult(false, "findings", [], ["fix"], false, null, false, false),
            new TestReport(false, [], ["failure"], [], [], true),
            1);

        Assert.That(input.Stage, Is.EqualTo("Tester"));
    }
}

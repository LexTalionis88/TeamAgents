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

    [Test]
    public void SecurityChallenge_CarriesArchitectureFindingToArchitect()
    {
        var architecture = new ArchitectureDecision(
            "Long-lived access token",
            ["Use bearer access token"],
            ["Simple client flow"],
            ["Authentication"],
            true,
            []);
        var security = new SecurityReview(
            false,
            ["Access token lifetime permits excessive replay"],
            ["Refresh token is not rotated"],
            ["Shorten access token lifetime and rotate refresh tokens"],
            true,
            true);

        var input = new ArchitectureRevisionRequest(architecture, security, 1);

        Assert.Multiple(() =>
        {
            Assert.That(input.Stage, Is.EqualTo("SecurityArchitectureChallenge"));
            Assert.That(input.CurrentArchitecture, Is.EqualTo(architecture));
            Assert.That(input.Security.ArchitectureChallenged, Is.True);
            Assert.That(input.Security.RequiredActions, Does.Contain("Shorten access token lifetime and rotate refresh tokens"));
        });
    }

    [Test]
    public void DeveloperTesterFixRequest_CarriesTesterFindingsToDeveloper()
    {
        var architecture = new ArchitectureDecision("Short URL", [], [], [], true, []);
        var implementation = new ImplementationResult(
            true, "implemented", ["API"], [], false, null, false, false,
            ["src/Api.cs"], ["dotnet build"], true, false, "build passed");
        var report = new TestReport(
            false, ["dotnet test"], ["redirect returns 500"], ["integration failure"],
            ["fix redirect path"], true);

        var request = new DeveloperTesterFixRequest(architecture, implementation, report, 2);

        Assert.Multiple(() =>
        {
            Assert.That(request.Stage, Is.EqualTo("ManagerDeveloperFix"));
            Assert.That(request.Cycle, Is.EqualTo(2));
            Assert.That(request.PreviousImplementation, Is.EqualTo(implementation));
            Assert.That(request.TesterReport.Findings, Does.Contain("redirect returns 500"));
        });
    }
}

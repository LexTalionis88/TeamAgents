using ModelContextProtocol.Client;

namespace Workspace.Tests.Integration;

[TestFixture]
[NonParallelizable]
public sealed class McpServerIntegrationTests
{
    [Test]
    public async Task Server_ExposesWorkspaceToolsOverStdio()
    {
        var project = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "../../../../../src/McpServer/McpServer.csproj"));

        Assert.That(File.Exists(project), Is.True, $"MCP project not found: {project}");

        await using var client = await McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = "workspace-mcp-test",
            Command = "dotnet",
            Arguments = ["run", "--project", project, "--no-launch-profile"],
        }));

        var tools = (await client.ListToolsAsync()).ToDictionary(tool => tool.Name);

        Assert.That(tools.Keys, Is.EquivalentTo(["get_workspace_status", "echo"]));

        var result = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object?> { ["text"] = "integration-ok" });

        Assert.That(result.IsError, Is.Not.True);
        Assert.That(result.Content.Single().ToString(), Does.Contain("integration-ok"));
    }
}

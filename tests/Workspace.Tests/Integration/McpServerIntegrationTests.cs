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

        Assert.That(tools.Keys, Does.Contain("get_workspace_status"));
        Assert.That(tools.Keys, Does.Contain("echo"));
        Assert.That(tools.Keys, Does.Contain("list_workspace_files"));
        Assert.That(tools.Keys, Does.Contain("read_workspace_file"));
        Assert.That(tools.Keys, Does.Contain("apply_workspace_patch"));
        Assert.That(tools.Keys, Does.Contain("run_dotnet_check"));
        Assert.That(tools.Keys, Does.Contain("get_workspace_diff"));
        Assert.That(tools.Keys, Does.Contain("get_workspace_evidence"));

        var result = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object?> { ["text"] = "integration-ok" });

        Assert.That(result.IsError, Is.Not.True);
        Assert.That(result.Content.Single().ToString(), Does.Contain("integration-ok"));
    }
}

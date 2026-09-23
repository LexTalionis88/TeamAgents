using AgentClient.Configuration;
using AgentClient.Infrastructure.Ai.Providers;
using AgentClient.Infrastructure.Mcp;
using Microsoft.Extensions.AI;

namespace Workspace.Tests.E2E;

[TestFixture]
public sealed class CloudflareSmokeTests
{
    /// <summary>
    /// Проверяет один реальный Cloudflare tool-call цикл через MCP без запуска полного workflow.
    /// </summary>
    [Test]
    [Explicit("Requires CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID.")]
    public async Task Cloudflare_CompletesMcpToolLoop()
    {
        var repository = FindRepositoryRoot();
        var options = AgentClientOptions.FromEnvironment() with
        {
            McpServerProject = Path.Combine(repository, "src", "McpServer", "McpServer.csproj"),
        };
        if (string.IsNullOrWhiteSpace(options.CloudflareApiToken) ||
            string.IsNullOrWhiteSpace(options.CloudflareAccountId))
        {
            Assert.Ignore("Set CLOUDFLARE_API_TOKEN and CLOUDFLARE_ACCOUNT_ID to run the Cloudflare smoke test.");
        }

        var provider = ChatClientProviderFactory.Resolve("cloudflare");
        await using var mcp = await McpServerConnection.ConnectAsync(options);
        var statusTool = mcp.Tools.Single(tool =>
            tool.Name.Equals("get_workspace_status", StringComparison.OrdinalIgnoreCase));
        using var client = new FunctionInvokingChatClient(provider.CreateChatClient(options))
        {
            MaximumIterationsPerRequest = 2,
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Вызови get_workspace_status и кратко сообщи результат.")],
            new ChatOptions
            {
                Tools = [statusTool],
                ToolMode = ChatToolMode.RequireAny,
                MaxOutputTokens = provider.MaxAgentOutputTokens,
            },
            timeout.Token);

        Assert.That(response.Text, Does.Contain("workspace"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "workspace.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("workspace.slnx was not found.");
    }
}

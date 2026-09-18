namespace AgentClient.Configuration;

public sealed record AgentClientOptions(
    string OllamaHost,
    string OllamaModel,
    string WorkflowFeature,
    string? McpServerDll,
    string McpServerProject)
{
    public static AgentClientOptions FromEnvironment()
    {
        var serverProject = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../McpServer/McpServer.csproj"));

        return new AgentClientOptions(
            Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434",
            Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen3:1.7b",
            Environment.GetEnvironmentVariable("WORKFLOW_FEATURE") ?? "workspace-architecture-review",
            Environment.GetEnvironmentVariable("MCP_SERVER_DLL"),
            serverProject);
    }
}

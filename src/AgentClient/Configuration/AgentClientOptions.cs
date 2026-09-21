namespace AgentClient.Configuration;

public sealed record AgentClientOptions(
    string ModelProvider,
    string OllamaHost,
    string OllamaModel,
    string OpenRouterModel,
    string OpenRouterBaseUrl,
    string? OpenRouterApiKey,
    string GeminiModel,
    string GeminiBaseUrl,
    string? GeminiApiKey,
    string WorkflowFeature,
    int MaxCycles,
    string? McpServerDll,
    string McpServerProject)
{
    public static AgentClientOptions FromEnvironment()
    {
        var serverProject = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../McpServer/McpServer.csproj"));

        var maxCycles = int.TryParse(
            Environment.GetEnvironmentVariable("WORKFLOW_MAX_CYCLES"), out var configuredCycles)
            ? Math.Clamp(configuredCycles, 0, 3)
            : 2;

        return new AgentClientOptions(
            Environment.GetEnvironmentVariable("MODEL_PROVIDER") ?? "ollama",
            Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434",
            Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen3:1.7b",
            Environment.GetEnvironmentVariable("OPENROUTER_MODEL") ?? "openrouter/free",
            Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL") ?? "https://openrouter.ai/api/v1",
            Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"),
            Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-3.8-flash",
            Environment.GetEnvironmentVariable("GEMINI_BASE_URL") ?? "https://generativelanguage.googleapis.com/v1beta/openai/",
            Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
            Environment.GetEnvironmentVariable("WORKFLOW_FEATURE") ?? "workspace-architecture-review",
            maxCycles,
            Environment.GetEnvironmentVariable("MCP_SERVER_DLL"),
            serverProject);
    }
}

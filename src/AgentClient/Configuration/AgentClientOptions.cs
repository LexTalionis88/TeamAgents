namespace AgentClient.Configuration;

internal sealed record AgentClientOptions(
    string ModelProvider,
    string OllamaHost,
    string OllamaModel,
    string OpenRouterModel,
    string OpenRouterBaseUrl,
    string? OpenRouterApiKey,
    string GeminiModel,
    string GeminiBaseUrl,
    string? GeminiApiKey,
    string GroqModel,
    string GroqBaseUrl,
    string? GroqApiKey,
    string WorkflowFeature,
    int MaxCycles,
    int MaxWorkItems,
    int AgentTimeoutSeconds,
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
        var maxWorkItems = int.TryParse(
            Environment.GetEnvironmentVariable("WORKFLOW_MAX_WORK_ITEMS"), out var configuredWorkItems)
            ? Math.Clamp(configuredWorkItems, 1, 6)
            : 6;
        var agentTimeoutSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("WORKFLOW_AGENT_TIMEOUT_SECONDS"), out var configuredTimeout)
            ? Math.Clamp(configuredTimeout, 30, 600)
            : 180;

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
            Environment.GetEnvironmentVariable("GROQ_MODEL") ?? "openai/gpt-oss-120b",
            Environment.GetEnvironmentVariable("GROQ_BASE_URL") ?? "https://api.groq.com/openai/v1",
            Environment.GetEnvironmentVariable("GROQ_API_KEY"),
            Environment.GetEnvironmentVariable("WORKFLOW_FEATURE") ?? "workspace-architecture-review",
            maxCycles,
            maxWorkItems,
            agentTimeoutSeconds,
            Environment.GetEnvironmentVariable("MCP_SERVER_DLL"),
            serverProject);
    }
}

using AgentClient.Configuration;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace AgentClient.Infrastructure.Mcp;

internal sealed class McpServerConnection : IAsyncDisposable
{
    private readonly McpClient _client;

    private McpServerConnection(McpClient client, IReadOnlyList<AITool> tools)
    {
        _client = client;
        Tools = tools;
    }

    public IReadOnlyList<AITool> Tools { get; }

    /// <summary>
    /// Запускает MCP-сервер, получает список его инструментов и создаёт соединение.
    /// </summary>
    /// <param name="options">Параметры запуска MCP-сервера.</param>
    /// <param name="cancellationToken">Токен отмены подключения.</param>
    public static async Task<McpServerConnection> ConnectAsync(
        AgentClientOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.McpServerDll) &&
            !File.Exists(options.McpServerProject))
        {
            throw new FileNotFoundException("Проект MCP-сервера не найден.", options.McpServerProject);
        }

        var arguments = string.IsNullOrWhiteSpace(options.McpServerDll)
            ? ["run", "--project", options.McpServerProject, "--no-launch-profile"]
            : new[] { options.McpServerDll! };

        var client = await McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = "workspace-mcp-server",
            Command = "dotnet",
            Arguments = arguments,
        }));

        var tools = (await client.ListToolsAsync())
            .Cast<AITool>()
            .ToArray();

        return new McpServerConnection(client, tools);
    }

    /// <summary>
    /// Освобождает MCP-клиент и закрывает stdio-транспорт.
    /// </summary>
    public ValueTask DisposeAsync() => _client.DisposeAsync();
}

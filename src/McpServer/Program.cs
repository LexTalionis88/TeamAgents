using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WorkspaceTools>();

await builder.Build().RunAsync();

[McpServerToolType]
public sealed class WorkspaceTools
{
    [McpServerTool, Description("Возвращает краткий статус MCP-сервера workspace.")]
    public string GetWorkspaceStatus() =>
        "MCP-сервер workspace запущен и предоставляет инструменты через stdio.";

    [McpServerTool, Description("Возвращает переданный текст без изменений.")]
    public string Echo(
        [Description("Текст, который нужно вернуть.")] string text) => text;
}

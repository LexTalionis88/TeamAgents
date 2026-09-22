using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
internal sealed class WorkspaceStatusTools
{
    [McpServerTool, Description("Возвращает краткий статус MCP-сервера workspace.")]
    public string GetWorkspaceStatus() =>
        "MCP-сервер workspace запущен и предоставляет инструменты через stdio.";

    [McpServerTool, Description("Возвращает переданный текст без изменений.")]
    public string Echo(
        [Description("Текст, который нужно вернуть.")] string text) => text;
}

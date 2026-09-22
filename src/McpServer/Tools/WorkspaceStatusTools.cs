using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
internal sealed class WorkspaceStatusTools
{
    /// <summary>
    /// Возвращает краткий статус MCP-сервера workspace.
    /// </summary>
    [McpServerTool, Description("Возвращает краткий статус MCP-сервера workspace.")]
    public string GetWorkspaceStatus() =>
        "MCP-сервер workspace запущен и предоставляет инструменты через stdio.";

    /// <summary>
    /// Возвращает переданный текст без изменений.
    /// </summary>
    /// <param name="text">Текст, который нужно вернуть.</param>
    [McpServerTool, Description("Возвращает переданный текст без изменений.")]
    public string Echo(
        [Description("Текст, который нужно вернуть.")] string text) => text;
}

namespace McpServer.Abstractions;

/// <summary>
/// Определяет правила разрешения и проверки путей workspace.
/// </summary>
internal interface IWorkspacePathResolver
{
    /// <summary>
    /// Возвращает корень workspace.
    /// </summary>
    string WorkspaceRoot { get; }

    /// <summary>
    /// Разрешает относительный путь внутри workspace.
    /// </summary>
    /// <param name="relativePath">Относительный путь.</param>
    /// <param name="mustExist">Требовать существование пути.</param>
    string ResolvePath(string relativePath, bool mustExist);

    /// <summary>
    /// Проверяет, что путь не относится к запрещённому служебному каталогу.
    /// </summary>
    /// <param name="path">Проверяемый путь.</param>
    bool IsAllowedPath(string path);
}

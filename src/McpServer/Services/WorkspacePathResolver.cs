using McpServer.Abstractions;

namespace McpServer.Services;

internal sealed class WorkspacePathResolver : IWorkspacePathResolver
{
    /// <summary>
    /// Создаёт резолвер относительно WORKSPACE_ROOT или текущего каталога.
    /// </summary>
    public WorkspacePathResolver()
        : this(Environment.GetEnvironmentVariable("WORKSPACE_ROOT") ?? Directory.GetCurrentDirectory())
    {
    }

    /// <summary>
    /// Создаёт резолвер с указанным корнем workspace.
    /// </summary>
    /// <param name="workspaceRoot">Абсолютный или относительный путь к корню workspace.</param>
    public WorkspacePathResolver(string workspaceRoot)
    {
        WorkspaceRoot = Path.GetFullPath(workspaceRoot);
    }

    /// <summary>
    /// Возвращает нормализованный корень workspace.
    /// </summary>
    public string WorkspaceRoot { get; }

    /// <summary>
    /// Разрешает относительный путь и проверяет его принадлежность workspace.
    /// </summary>
    /// <param name="relativePath">Относительный путь внутри workspace.</param>
    /// <param name="mustExist">Требовать существование файла или каталога.</param>
    public string ResolvePath(string relativePath, bool mustExist)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Используй относительный путь workspace.", nameof(relativePath));
        }

        var fullPath = Path.GetFullPath(Path.Combine(WorkspaceRoot, relativePath));
        if (!fullPath.StartsWith(WorkspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, WorkspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Путь выходит за пределы workspace.");
        }

        if (mustExist && !File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw new FileNotFoundException("Путь workspace не найден.", relativePath);
        }

        return fullPath;
    }

    /// <summary>
    /// Проверяет, что путь не проходит через .git, bin или obj.
    /// </summary>
    /// <param name="path">Путь, который нужно проверить.</param>
    public bool IsAllowedPath(string path) =>
        !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is ".git" or "bin" or "obj");
}

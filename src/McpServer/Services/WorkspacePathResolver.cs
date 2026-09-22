using McpServer.Abstractions;

namespace McpServer.Services;

internal sealed class WorkspacePathResolver : IWorkspacePathResolver
{
    public WorkspacePathResolver()
        : this(Environment.GetEnvironmentVariable("WORKSPACE_ROOT") ?? Directory.GetCurrentDirectory())
    {
    }

    public WorkspacePathResolver(string workspaceRoot)
    {
        WorkspaceRoot = Path.GetFullPath(workspaceRoot);
    }

    public string WorkspaceRoot { get; }

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

    public bool IsAllowedPath(string path) =>
        !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is ".git" or "bin" or "obj");
}

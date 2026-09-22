namespace McpServer.Abstractions;

internal interface IWorkspacePathResolver
{
    string WorkspaceRoot { get; }

    string ResolvePath(string relativePath, bool mustExist);

    bool IsAllowedPath(string path);
}

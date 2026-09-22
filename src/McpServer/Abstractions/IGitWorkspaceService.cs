namespace McpServer.Abstractions;

internal interface IGitWorkspaceService
{
    Task<string> ApplyPatchAsync(string patch, CancellationToken cancellationToken);

    Task<string> GetRevisionAsync(CancellationToken cancellationToken);
}

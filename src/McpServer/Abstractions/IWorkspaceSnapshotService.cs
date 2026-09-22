using McpServer.Models;

namespace McpServer.Abstractions;

internal interface IWorkspaceSnapshotService
{
    Task<WorkspaceSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}

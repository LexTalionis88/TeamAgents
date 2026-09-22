using McpServer.Models;

namespace McpServer.Abstractions;

/// <summary>
/// Определяет получение снимка изменений workspace.
/// </summary>
internal interface IWorkspaceSnapshotService
{
    /// <summary>
    /// Собирает tracked и untracked изменения workspace в единый снимок.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены получения снимка.</param>
    Task<WorkspaceSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}

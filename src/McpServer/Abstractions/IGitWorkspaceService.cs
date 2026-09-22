namespace McpServer.Abstractions;

/// <summary>
/// Определяет безопасные операции изменения и идентификации git workspace.
/// </summary>
internal interface IGitWorkspaceService
{
    /// <summary>
    /// Проверяет и применяет patch к workspace.
    /// </summary>
    /// <param name="patch">Patch в разрешённом формате.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    Task<string> ApplyPatchAsync(string patch, CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает текущую ревизию git workspace.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены команды git.</param>
    Task<string> GetRevisionAsync(CancellationToken cancellationToken);
}

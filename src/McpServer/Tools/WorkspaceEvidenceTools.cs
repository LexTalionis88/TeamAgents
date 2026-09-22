using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using McpServer.Abstractions;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
internal sealed class WorkspaceEvidenceTools(
    IGitWorkspaceService gitWorkspaceService,
    IWorkspaceSnapshotService snapshotService)
{
    /// <summary>
    /// Возвращает текущий diff workspace.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены получения diff.</param>
    [McpServerTool, Description("Возвращает текущий git diff workspace для проверки Developer, Tester и Reviewer.")]
    public async Task<string> GetWorkspaceDiff(CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotService.GetSnapshotAsync(cancellationToken);
        return snapshot.Diff;
    }

    /// <summary>
    /// Возвращает ревизию, хеш diff и список изменённых файлов workspace.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены сбора evidence.</param>
    [McpServerTool, Description("Возвращает машиночитаемые доказательства workspace: git revision, SHA-256 текущего diff и список изменённых файлов.")]
    public async Task<string> GetWorkspaceEvidence(CancellationToken cancellationToken = default)
    {
        var revision = (await gitWorkspaceService.GetRevisionAsync(cancellationToken)).Trim();
        var snapshot = await snapshotService.GetSnapshotAsync(cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Diff))).ToLowerInvariant();

        return JsonSerializer.Serialize(new
        {
            workspaceRevision = revision,
            diffHash = $"sha256:{hash}",
            changedFiles = snapshot.ChangedFiles,
        });
    }
}

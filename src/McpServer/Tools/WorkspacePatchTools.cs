using System.ComponentModel;
using McpServer.Abstractions;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
internal sealed class WorkspacePatchTools(IGitWorkspaceService gitWorkspaceService)
{
    [McpServerTool, Description("Применяет unified diff к workspace после проверки git apply --check. Также принимает формат *** Begin Patch для добавления новых файлов. Не принимает абсолютные пути.")]
    public Task<string> ApplyWorkspacePatch(
        [Description("Патч в формате git diff или *** Begin Patch. Для изменения существующего файла можно использовать replace_workspace_file.")] string patch,
        CancellationToken cancellationToken = default) =>
        gitWorkspaceService.ApplyPatchAsync(patch, cancellationToken);
}

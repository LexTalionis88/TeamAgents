using System.Text;
using McpServer.Abstractions;
using McpServer.Models;

namespace McpServer.Services;

internal sealed class WorkspaceSnapshotService(
    IWorkspacePathResolver pathResolver,
    IProcessRunner processRunner) : IWorkspaceSnapshotService
{
    public async Task<WorkspaceSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var trackedDiff = await processRunner.RunTextAsync(
            "git",
            ["diff", "--", "."],
            cancellationToken);
        var trackedFiles = (await processRunner.RunTextAsync(
                "git",
                ["diff", "--name-only", "--", "."],
                cancellationToken))
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var status = await processRunner.RunTextAsync(
            "git",
            ["status", "--short", "--untracked-files=all"],
            cancellationToken);
        var untrackedFiles = status
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("?? ", StringComparison.Ordinal))
            .Select(line => line[3..].Trim())
            .Where(pathResolver.IsAllowedPath)
            .ToArray();

        var diff = new StringBuilder(trackedDiff);
        foreach (var relativePath in untrackedFiles)
        {
            var untrackedDiff = await processRunner.RunTextAsync(
                "git",
                ["diff", "--no-index", "--", "/dev/null", relativePath],
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(untrackedDiff) &&
                !untrackedDiff.Contains("fatal:", StringComparison.OrdinalIgnoreCase))
            {
                diff.AppendLine(untrackedDiff);
                continue;
            }

            var fullPath = pathResolver.ResolvePath(relativePath, mustExist: true);
            diff.AppendLine($"diff --git a/{relativePath} b/{relativePath}");
            diff.AppendLine("new file mode 100644");
            diff.AppendLine("--- /dev/null");
            diff.AppendLine($"+++ b/{relativePath}");
            foreach (var line in await File.ReadAllLinesAsync(fullPath, cancellationToken))
            {
                diff.Append('+').AppendLine(line);
            }
        }

        var changedFiles = trackedFiles
            .Concat(untrackedFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new WorkspaceSnapshot(diff.ToString(), changedFiles);
    }
}

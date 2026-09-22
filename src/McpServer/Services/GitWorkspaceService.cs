using System.Text;
using McpServer.Abstractions;

namespace McpServer.Services;

internal sealed class GitWorkspaceService(
    IWorkspacePathResolver pathResolver,
    IProcessRunner processRunner) : IGitWorkspaceService
{
    public async Task<string> ApplyPatchAsync(
        string patch,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(patch) || patch.Contains(".git/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Patch пустой или затрагивает запрещённый путь.", nameof(patch));
        }

        var applyPatchFormatResult = await TryApplyPatchFormatAsync(patch, cancellationToken);
        if (applyPatchFormatResult is not null)
        {
            return applyPatchFormatResult;
        }

        var check = await processRunner.RunAsync(
            "git",
            ["apply", "--check", "--whitespace=nowarn", "-"],
            patch,
            cancellationToken);
        if (check.ExitCode != 0)
        {
            var untrackedFileResult = await TryApplyUntrackedNewFileAsync(patch, cancellationToken);
            if (untrackedFileResult is not null)
            {
                return untrackedFileResult;
            }

            return $"PATCH_REJECTED\n{check.Output}";
        }

        var applied = await processRunner.RunAsync(
            "git",
            ["apply", "--whitespace=nowarn", "-"],
            patch,
            cancellationToken);
        return applied.ExitCode == 0
            ? "PATCH_APPLIED"
            : $"PATCH_FAILED\n{applied.Output}";
    }

    public Task<string> GetRevisionAsync(CancellationToken cancellationToken) =>
        processRunner.RunTextAsync("git", ["rev-parse", "HEAD"], cancellationToken);

    private async Task<string?> TryApplyUntrackedNewFileAsync(
        string patch,
        CancellationToken cancellationToken)
    {
        if (patch.Contains("*** Begin Patch", StringComparison.Ordinal) ||
            patch.Contains("*** End Patch", StringComparison.Ordinal))
        {
            return null;
        }

        var lines = patch.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Count(line => line.StartsWith("diff --git ", StringComparison.Ordinal)) != 1 ||
            !lines.Any(line => line.StartsWith("new file mode ", StringComparison.Ordinal)))
        {
            return null;
        }

        var header = lines.First(line => line.StartsWith("diff --git ", StringComparison.Ordinal));
        var headerParts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (headerParts.Length < 4 || !headerParts[3].StartsWith("b/", StringComparison.Ordinal))
        {
            return null;
        }

        var relativePath = headerParts[3][2..].Replace('\\', '/');
        if (!pathResolver.IsAllowedPath(relativePath) || relativePath.Contains("..", StringComparison.Ordinal) ||
            !lines.Contains($"+++ b/{relativePath}", StringComparer.Ordinal))
        {
            return null;
        }

        var hunkIndex = Array.FindIndex(lines, line => line.StartsWith("@@", StringComparison.Ordinal));
        if (hunkIndex < 0)
        {
            return null;
        }

        var fullPath = pathResolver.ResolvePath(relativePath, mustExist: false);
        if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 0)
        {
            return "PATCH_REJECTED\nPatch нового файла указывает на существующий файл workspace.";
        }

        var contentLines = new List<string>();
        for (var index = hunkIndex + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.StartsWith("\\ No newline", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("diff --git ", StringComparison.Ordinal) ||
                line.StartsWith("--- ", StringComparison.Ordinal) ||
                line.StartsWith("+++ ", StringComparison.Ordinal) ||
                line.StartsWith("@@", StringComparison.Ordinal) ||
                line.StartsWith("-", StringComparison.Ordinal))
            {
                return null;
            }

            contentLines.Add(line.StartsWith('+') || line.StartsWith(' ')
                ? line[1..]
                : line);
        }

        if (contentLines.Count == 0 || contentLines.All(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            fullPath,
            string.Join('\n', contentLines),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        return "PATCH_APPLIED";
    }

    private async Task<string?> TryApplyPatchFormatAsync(
        string patch,
        CancellationToken cancellationToken)
    {
        var lines = patch.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (!lines.Contains("*** Begin Patch", StringComparer.Ordinal) ||
            !lines.Contains("*** End Patch", StringComparer.Ordinal))
        {
            return null;
        }

        var beginIndex = Array.IndexOf(lines, "*** Begin Patch");
        var endIndex = Array.LastIndexOf(lines, "*** End Patch");
        if (endIndex <= beginIndex)
        {
            return "PATCH_REJECTED\nФормат патча содержит некорректные границы.";
        }

        var fileMarkers = lines
            .Select((line, index) => (line, index))
            .Where(item => item.index > beginIndex && item.index < endIndex &&
                           item.line.StartsWith("*** ", StringComparison.Ordinal))
            .ToArray();
        if (fileMarkers.Length == 0)
        {
            return "PATCH_REJECTED\nПатч не содержит операций над файлами.";
        }

        var pendingFiles = new List<(string Path, string Content)>();
        for (var markerIndex = 0; markerIndex < fileMarkers.Length; markerIndex++)
        {
            var (marker, lineIndex) = fileMarkers[markerIndex];
            var nextIndex = markerIndex + 1 < fileMarkers.Length
                ? fileMarkers[markerIndex + 1].index
                : endIndex;

            if (!marker.StartsWith("*** Add File: ", StringComparison.Ordinal))
            {
                return "PATCH_REJECTED\nПоддерживается только добавление новых файлов в формате Begin/End Patch. Для изменения существующего файла используйте replace_workspace_file или unified diff.";
            }

            var relativePath = marker["*** Add File: ".Length..].Trim();
            if (string.IsNullOrWhiteSpace(relativePath) || !pathResolver.IsAllowedPath(relativePath) ||
                relativePath.Contains("..", StringComparison.Ordinal))
            {
                return "PATCH_REJECTED\nПатч содержит недопустимый относительный путь.";
            }

            var fullPath = pathResolver.ResolvePath(relativePath, mustExist: false);
            if (File.Exists(fullPath))
            {
                return $"PATCH_REJECTED\nФайл {relativePath} уже существует; используйте replace_workspace_file.";
            }

            var contentLines = lines[(lineIndex + 1)..nextIndex].ToList();
            if (contentLines.All(line => string.IsNullOrEmpty(line) || line.StartsWith('+')))
            {
                contentLines = contentLines
                    .Select(line => line.StartsWith('+') ? line[1..] : line)
                    .ToList();
            }

            var content = string.Join('\n', contentLines);
            if (content.Length > 512_000)
            {
                return "PATCH_REJECTED\nРазмер нового файла превышает лимит 512 KB.";
            }

            pendingFiles.Add((relativePath, content));
        }

        foreach (var (relativePath, content) in pendingFiles)
        {
            var fullPath = pathResolver.ResolvePath(relativePath, mustExist: false);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(
                fullPath,
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
        }

        return "PATCH_APPLIED";
    }
}

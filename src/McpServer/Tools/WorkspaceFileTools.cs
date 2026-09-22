using System.ComponentModel;
using System.Text;
using McpServer.Abstractions;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
internal sealed class WorkspaceFileTools(IWorkspacePathResolver pathResolver)
{
    [McpServerTool, Description("Список исходных файлов workspace. Возвращает только относительные пути и исключает bin, obj и .git.")]
    public string ListWorkspaceFiles(
        [Description("Относительный каталог внутри workspace, по умолчанию корень.")] string relativeDirectory = ".")
    {
        var directory = pathResolver.ResolvePath(relativeDirectory, mustExist: true);
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(pathResolver.IsAllowedPath)
            .Select(path => Path.GetRelativePath(pathResolver.WorkspaceRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Take(500);
        return string.Join(Environment.NewLine, files);
    }

    [McpServerTool, Description("Читает текстовый файл workspace по относительному пути. Используй перед изменением файла.")]
    public string ReadWorkspaceFile(
        [Description("Относительный путь файла внутри workspace.")] string relativePath)
    {
        var path = pathResolver.ResolvePath(relativePath, mustExist: true);
        if (!pathResolver.IsAllowedPath(path) || new FileInfo(path).Length > 512_000)
        {
            throw new InvalidOperationException("Файл запрещён или превышает лимит 512 KB.");
        }

        return File.ReadAllText(path);
    }

    [McpServerTool, Description("Заменяет содержимое текстового файла workspace для детерминированной подготовки.")]
    public async Task<string> ReplaceWorkspaceFile(
        [Description("Относительный путь текстового файла внутри workspace.")] string relativePath,
        [Description("Полное новое содержимое файла.")] string content,
        CancellationToken cancellationToken = default)
    {
        var path = pathResolver.ResolvePath(relativePath, mustExist: false);
        if (!pathResolver.IsAllowedPath(path) || content.Length > 512_000)
        {
            throw new InvalidOperationException("Путь к файлу запрещён или содержимое превышает лимит 512 KB.");
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            path,
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        return "FILE_REPLACED";
    }
}

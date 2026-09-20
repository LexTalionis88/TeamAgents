using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WorkspaceTools>();

await builder.Build().RunAsync();

[McpServerToolType]
public sealed class WorkspaceTools
{
    private static readonly string WorkspaceRoot = Path.GetFullPath(
        Environment.GetEnvironmentVariable("WORKSPACE_ROOT") ?? Directory.GetCurrentDirectory());

    [McpServerTool, Description("Возвращает краткий статус MCP-сервера workspace.")]
    public string GetWorkspaceStatus() =>
        "MCP-сервер workspace запущен и предоставляет инструменты через stdio.";

    [McpServerTool, Description("Возвращает переданный текст без изменений.")]
    public string Echo(
        [Description("Текст, который нужно вернуть.")] string text) => text;

    [McpServerTool, Description("Список исходных файлов workspace. Возвращает только относительные пути и исключает bin, obj и .git.")]
    public string ListWorkspaceFiles(
        [Description("Относительный каталог внутри workspace, по умолчанию корень.")] string relativeDirectory = ".")
    {
        var directory = ResolvePath(relativeDirectory, mustExist: true);
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(IsAllowedPath)
            .Select(path => Path.GetRelativePath(WorkspaceRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Take(500);
        return string.Join(Environment.NewLine, files);
    }

    [McpServerTool, Description("Читает текстовый файл workspace по относительному пути. Используй перед изменением файла.")]
    public string ReadWorkspaceFile(
        [Description("Относительный путь файла внутри workspace.")] string relativePath)
    {
        var path = ResolvePath(relativePath, mustExist: true);
        if (!IsAllowedPath(path) || new FileInfo(path).Length > 512_000)
        {
            throw new InvalidOperationException("Файл запрещён или превышает лимит 512 KB.");
        }

        return File.ReadAllText(path);
    }

    [McpServerTool, Description("Применяет unified diff к workspace после git apply --check. Не принимает абсолютные пути.")]
    public async Task<string> ApplyWorkspacePatch(
        [Description("Unified diff в формате git apply.")] string patch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(patch) || patch.Contains(".git/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Patch пустой или затрагивает запрещённый путь.", nameof(patch));
        }

        var check = await RunProcessAsync("git", ["apply", "--check", "--whitespace=nowarn", "-"], patch, cancellationToken);
        if (check.ExitCode != 0)
        {
            return $"PATCH_REJECTED\n{check.Output}";
        }

        var applied = await RunProcessAsync("git", ["apply", "--whitespace=nowarn", "-"], patch, cancellationToken);
        return applied.ExitCode == 0
            ? "PATCH_APPLIED"
            : $"PATCH_FAILED\n{applied.Output}";
    }

    [McpServerTool, Description("Запускает разрешённую .NET-проверку в workspace: dotnet restore, build или test.")]
    public Task<string> RunDotnetCheck(
        [Description("Одна из команд: dotnet restore, dotnet build, dotnet test; допускаются только аргументы workspace.slnx и --no-restore.")] string command,
        CancellationToken cancellationToken = default)
    {
        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 || tokens[0] != "dotnet" ||
            tokens.Skip(1).Any(token => token is not ("restore" or "build" or "test" or "workspace.slnx" or "--no-restore")))
        {
            throw new ArgumentException("Разрешены только dotnet restore/build/test с workspace.slnx и --no-restore.", nameof(command));
        }

        return RunProcessTextAsync("dotnet", tokens[1..], cancellationToken);
    }

    [McpServerTool, Description("Возвращает текущий git diff workspace для проверки Developer/Tester/Reviewer.")]
    public Task<string> GetWorkspaceDiff(CancellationToken cancellationToken = default) =>
        RunProcessTextAsync("git", ["diff", "--", "."], cancellationToken);

    [McpServerTool, Description("Возвращает machine-readable доказательства workspace: git revision, SHA-256 текущего diff и список изменённых файлов.")]
    public async Task<string> GetWorkspaceEvidence(CancellationToken cancellationToken = default)
    {
        var revision = (await RunProcessTextAsync("git", ["rev-parse", "HEAD"], cancellationToken)).Trim();
        var diff = await RunProcessTextAsync("git", ["diff", "--", "."], cancellationToken);
        var changedFiles = (await RunProcessTextAsync("git", ["diff", "--name-only", "--", "."], cancellationToken))
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(diff))).ToLowerInvariant();

        return JsonSerializer.Serialize(new
        {
            workspaceRevision = revision,
            diffHash = $"sha256:{hash}",
            changedFiles,
        });
    }

    private static string ResolvePath(string relativePath, bool mustExist)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Используй относительный путь workspace.", nameof(relativePath));
        }

        var fullPath = Path.GetFullPath(Path.Combine(WorkspaceRoot, relativePath));
        if (!fullPath.StartsWith(WorkspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, WorkspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Путь выходит за пределы workspace.");
        }

        if (mustExist && !File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw new FileNotFoundException("Путь workspace не найден.", relativePath);
        }

        return fullPath;
    }

    private static bool IsAllowedPath(string path) =>
        !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is ".git" or "bin" or "obj");

    private static async Task<string> RunProcessTextAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        (await RunProcessAsync(fileName, arguments, null, cancellationToken)).Output;

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = WorkspaceRoot,
                RedirectStandardInput = standardInput is not null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, (await stdout) + (await stderr));
    }
}

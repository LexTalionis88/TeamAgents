using System.ComponentModel;
using McpServer.Abstractions;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

[McpServerToolType]
internal sealed class DotnetCheckTools(IProcessRunner processRunner)
{
    /// <summary>
    /// Запускает одну из разрешённых проверок solution.
    /// </summary>
    /// <param name="command">Разрешённая команда dotnet с аргументами workspace.slnx.</param>
    /// <param name="cancellationToken">Токен отмены проверки.</param>
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

        return processRunner.RunTextAsync("dotnet", tokens[1..], cancellationToken);
    }
}

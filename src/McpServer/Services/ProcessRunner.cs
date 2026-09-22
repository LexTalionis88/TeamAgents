using System.Diagnostics;
using McpServer.Abstractions;
using McpServer.Models;

namespace McpServer.Services;

internal sealed class ProcessRunner(IWorkspacePathResolver pathResolver) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
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
                WorkingDirectory = pathResolver.WorkspaceRoot,
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
        return new ProcessResult(process.ExitCode, (await stdout) + (await stderr));
    }

    public async Task<string> RunTextAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        (await RunAsync(fileName, arguments, standardInput: null, cancellationToken)).Output;
}

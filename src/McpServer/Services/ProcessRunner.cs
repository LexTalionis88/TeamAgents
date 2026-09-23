using System.Diagnostics;
using McpServer.Abstractions;
using McpServer.Models;

namespace McpServer.Services;

internal sealed class ProcessRunner(IWorkspacePathResolver pathResolver) : IProcessRunner
{
    /// <summary>
    /// Запускает процесс в корне workspace и возвращает код завершения с выводом.
    /// </summary>
    /// <param name="fileName">Имя исполняемой команды.</param>
    /// <param name="arguments">Аргументы процесса.</param>
    /// <param name="standardInput">Необязательный текст стандартного ввода.</param>
    /// <param name="cancellationToken">Токен отмены процесса.</param>
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
                RedirectStandardInput = true,
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
        try
        {
            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            }
            process.StandardInput.Close();

            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new ProcessResult(process.ExitCode, (await stdout) + (await stderr));
        }
        catch
        {
            StopProcess(process);
            try
            {
                await process.WaitForExitAsync();
            }
            catch
            {
                // Сохраняем исходную ошибку отмены или запуска процесса.
            }

            throw;
        }
    }

    /// <summary>
    /// Запускает процесс и возвращает объединённый текстовый вывод.
    /// </summary>
    /// <param name="fileName">Имя исполняемой команды.</param>
    /// <param name="arguments">Аргументы процесса.</param>
    /// <param name="cancellationToken">Токен отмены процесса.</param>
    public async Task<string> RunTextAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        (await RunAsync(fileName, arguments, standardInput: null, cancellationToken)).Output;

    private static void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Процесс уже завершился между проверкой и Kill.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // ОС не позволила повторно открыть уже завершившийся процесс.
        }
    }
}

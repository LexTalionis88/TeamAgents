using McpServer.Models;

namespace McpServer.Abstractions;

/// <summary>
/// Определяет запуск внешних процессов из корня workspace.
/// </summary>
internal interface IProcessRunner
{
    /// <summary>
    /// Запускает процесс и возвращает код завершения с объединённым выводом.
    /// </summary>
    /// <param name="fileName">Имя исполняемой команды.</param>
    /// <param name="arguments">Аргументы процесса.</param>
    /// <param name="standardInput">Необязательный текст стандартного ввода.</param>
    /// <param name="cancellationToken">Токен отмены процесса.</param>
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken);

    /// <summary>
    /// Запускает процесс и возвращает объединённый текстовый вывод.
    /// </summary>
    /// <param name="fileName">Имя исполняемой команды.</param>
    /// <param name="arguments">Аргументы процесса.</param>
    /// <param name="cancellationToken">Токен отмены процесса.</param>
    Task<string> RunTextAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}

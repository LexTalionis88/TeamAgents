using System.Diagnostics;

namespace Workspace.Tests.E2E;

[TestFixture]
public sealed class AgentWorkflowE2ETests
{
    [Test]
    [Explicit("Requires a running Ollama instance and a tool-calling model. Set RUN_E2E_TESTS=true.")]
    public async Task AgentClient_CompletesWorkflowThroughMcp()
    {
        var output = await RunAgentAsync("Проверь статус MCP-сервера");

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Подключение к MCP-серверу выполнено"));
            Assert.That(output, Does.Contain("[WORKFLOW] Завершено"));
            Assert.That(output, Does.Contain("correlation_id="));
        });
    }

    [Test]
    [Explicit("Requires a running Ollama instance and a tool-calling model. Set RUN_E2E_TESTS=true.")]
    public async Task AgentClient_ReturnsSecurityFindingToArchitectForSuspiciousTokenLifetime()
    {
        var output = await RunAgentAsync(
            "Проверь сомнительную архитектурную схему auth/token lifetime: access token живёт слишком долго, refresh token не ротируется. Security должен оспорить решение и вернуть finding Architect для пересмотра.");

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("[WORKFLOW] Завершено"));
            Assert.That(output, Does.Contain("transition:Security->Architect"),
                "Security должен вернуть архитектурный finding Architect.");
        });
    }

    private static async Task<string> RunAgentAsync(string prompt)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_E2E_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("Set RUN_E2E_TESTS=true to run the Ollama-backed E2E test.");
        }

        var repository = FindRepositoryRoot();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repository,
                ArgumentList =
                {
                    "run", "--project", "src/AgentClient", "--no-launch-profile", "--", prompt,
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);
        var output = await outputTask;
        var error = await errorTask;

        Assert.That(process.ExitCode, Is.EqualTo(0), $"stderr: {error}");
        return output;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "workspace.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("workspace.slnx was not found.");
    }
}

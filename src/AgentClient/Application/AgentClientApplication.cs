using AgentClient.Agents;
using AgentClient.Configuration;
using AgentClient.Infrastructure.Mcp;
using AgentClient.Infrastructure.Observability;
using AgentClient.Workflow;
using Microsoft.Agents.AI.Workflows;
using OllamaSharp;

namespace AgentClient.Application;

public sealed class AgentClientApplication
{
    public async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var options = AgentClientOptions.FromEnvironment();
        await using var mcp = await McpServerConnection.ConnectAsync(options, cancellationToken);
        Console.WriteLine($"Подключение к MCP-серверу выполнено. Инструменты: {string.Join(", ", mcp.Tools.Select(tool => tool.Name))}");

        using var telemetry = TelemetryScope.Create();
        using var chatClient = new OllamaApiClient(new Uri(options.OllamaHost), options.OllamaModel);
        var metadata = WorkflowRunMetadata.Create(options.WorkflowFeature);
        using var runScope = telemetry.StartRun(metadata);
        var agents = AgentFactory.Create(chatClient, mcp.Tools.ToList());
        var question = CreateQuestion(args);

        Console.WriteLine(
            $"Запуск workflow: task_id={metadata.TaskId}; " +
            $"correlation_id={metadata.CorrelationId}; feature={metadata.Feature}");
        Console.WriteLine($"Задача: {question.Question}");
        Console.WriteLine("Цепочка: Manager -> Architect -> Developer -> Tester -> Security -> Reviewer -> Manager");

        var workflow = new EscalatingWorkflow(
            agents,
            metadata,
            telemetry.WorkflowActivitySource,
            options.MaxCycles);
        var result = await workflow.RunAsync(question);
        Console.WriteLine($"[WORKFLOW] Завершено: approved={result.Approved}; summary={result.Summary}");
    }

    private static ArchitectureQuestion CreateQuestion(string[] args)
    {
        var prompt = args.Length > 0
            ? string.Join(' ', args)
            : "Как безопасно добавить новый MCP-инструмент в workspace?";

        return new ArchitectureQuestion(
            prompt,
            "Минимальный workspace на .NET с MCP Server, AgentClient, локальным Ollama и Docker Compose.",
            ["не использовать облачные модели", "сохранить русскоязычную документацию", "не нарушить MCP stdio transport"]);
    }
}

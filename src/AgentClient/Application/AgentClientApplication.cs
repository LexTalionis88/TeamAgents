using AgentClient.Agents;
using AgentClient.Configuration;
using AgentClient.Infrastructure.Ai.Abstractions;
using AgentClient.Infrastructure.Ai.Providers;
using AgentClient.Infrastructure.Mcp;
using AgentClient.Infrastructure.Observability;
using AgentClient.Workflow;

namespace AgentClient.Application;

internal sealed class AgentClientApplication
{
    public async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var options = AgentClientOptions.FromEnvironment();
        await using var mcp = await McpServerConnection.ConnectAsync(options, cancellationToken);
        Console.WriteLine(
            $"Соединение с MCP установлено. Инструменты: {string.Join(", ", mcp.Tools.Select(tool => tool.Name))}");

        using var telemetry = TelemetryScope.Create();
        var provider = ChatClientProviderFactory.Resolve(options.ModelProvider);
        using var chatClient = provider.CreateChatClient(options);
        var metadata = WorkflowRunMetadata.Create(options.WorkflowFeature);
        using var runScope = telemetry.StartRun(metadata);
        var agents = AgentFactory.Create(
            chatClient,
            mcp.Tools.ToList(),
            provider.MaxAgentOutputTokens,
            provider.PreferCompactAgentInstructions);
        var question = CreateQuestion(args, options, provider);

        Console.WriteLine(
            $"Запуск workflow: task_id={metadata.TaskId}; " +
            $"correlation_id={metadata.CorrelationId}; feature={metadata.Feature}");
        Console.WriteLine($"Задача: {question.Question}");
        Console.WriteLine("Цепочка: Manager -> Architect -> Developer -> Tester -> Security -> Reviewer -> Manager");

        var workflow = new EscalatingWorkflow(
            agents,
            metadata,
            telemetry.WorkflowActivitySource,
            provider,
            options.MaxCycles,
            options.MaxWorkItems,
            options.AgentTimeoutSeconds);
        try
        {
            var result = await workflow.RunAsync(question);
            Console.WriteLine($"[WORKFLOW] Завершено: approved={result.Approved}; summary={result.Summary}");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"[WORKFLOW] Ошибка: task_id={metadata.TaskId}; " +
                $"correlation_id={metadata.CorrelationId}; provider={provider.Name}; " +
                $"type={exception.GetType().Name}; message={exception.Message}");
            throw;
        }
    }

    private static ArchitectureQuestion CreateQuestion(
        string[] args,
        AgentClientOptions options,
        IChatClientProvider provider)
    {
        var prompt = args.Length > 0
            ? string.Join(' ', args)
            : "Как безопасно реализовать запрошенное изменение в workspace?";

        return new ArchitectureQuestion(
            prompt,
            $"Минимальный .NET workspace с MCP Server, AgentClient, {provider.Describe(options)} и Docker Compose.",
            [
                provider.CredentialConstraint,
                "сохранять язык документации репозитория",
                "не нарушать MCP stdio-транспорт",
            ]);
    }
}

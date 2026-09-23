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
    /// <summary>
    /// Запускает MCP-соединение, выбранный provider и общий workflow.
    /// </summary>
    /// <param name="args">Аргументы командной строки с исходной задачей.</param>
    /// <param name="cancellationToken">Токен отмены запуска приложения.</param>
    public async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var options = AgentClientOptions.FromEnvironment();
        using var workflowTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        workflowTimeout.CancelAfter(TimeSpan.FromSeconds(options.WorkflowTimeoutSeconds));
        var workflowCancellationToken = workflowTimeout.Token;

        await using var mcp = await McpServerConnection.ConnectAsync(options, workflowCancellationToken);
        Console.WriteLine("Соединение с MCP установлено.");
        Console.WriteLine($"Режим workspace: {(options.WorkflowReadOnly ? "read-only" : "read-write")}");

        using var telemetry = TelemetryScope.Create();
        var provider = ChatClientProviderFactory.Resolve(options.ModelProvider);
        using var chatClient = provider.CreateChatClient(options);
        var metadata = WorkflowRunMetadata.Create(options.WorkflowFeature);
        using var runScope = telemetry.StartRun(metadata);
        var agents = AgentFactory.Create(
            chatClient,
            mcp.Tools.ToList(),
            provider.MaxAgentOutputTokens,
            provider.PreferCompactAgentInstructions,
            options.WorkflowReadOnly);
        Console.WriteLine(
            $"Инструменты агентов: {string.Join(", ", agents.Tools?.Select(tool => tool.Name) ?? Array.Empty<string>())}");
        var question = CreateQuestion(args, options, provider);

        Console.WriteLine(
            $"Запуск workflow: task_id={metadata.TaskId}; " +
            $"correlation_id={metadata.CorrelationId}; feature={metadata.Feature}");
        Console.WriteLine($"Задача: {question.Question}");
        Console.WriteLine(
            options.WorkflowOrchestration == "magentic"
                ? "Цепочка: MagenticManager -> Architect/Developer/Tester/Security/Reviewer -> typed Reviewer gate"
                : "Цепочка: Manager -> Architect -> Developer -> Tester -> Security -> Reviewer -> Manager");

        Console.WriteLine($"Оркестрация: {options.WorkflowOrchestration}");

        IWorkflowRunner workflow = options.WorkflowOrchestration switch
        {
            "magentic" => new MagenticWorkflow(
                agents,
                metadata,
                telemetry.WorkflowActivitySource,
                provider,
                options.MaxCycles,
                options.MaxWorkItems,
                options.AgentTimeoutSeconds,
                workflowCancellationToken,
                options.WorkflowReadOnly),
            _ => new EscalatingWorkflow(
                agents,
                metadata,
                telemetry.WorkflowActivitySource,
                provider,
                options.MaxCycles,
                options.MaxWorkItems,
                options.AgentTimeoutSeconds,
                workflowCancellationToken,
                options.WorkflowReadOnly),
        };
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

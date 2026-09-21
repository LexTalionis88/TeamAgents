using AgentClient.Agents;
using AgentClient.Configuration;
using AgentClient.Infrastructure.Mcp;
using AgentClient.Infrastructure.Observability;
using AgentClient.Infrastructure.Ai;
using AgentClient.Workflow;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
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
        using var chatClient = CreateChatClient(options);
        var metadata = WorkflowRunMetadata.Create(options.WorkflowFeature);
        using var runScope = telemetry.StartRun(metadata);
        var agents = AgentFactory.Create(chatClient, mcp.Tools.ToList());
        var question = CreateQuestion(args, options);

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

    private static IChatClient CreateChatClient(AgentClientOptions options)
    {
        if (options.ModelProvider.Equals("gemini", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.GeminiApiKey))
            {
                throw new InvalidOperationException(
                    "MODEL_PROVIDER=gemini требует GEMINI_API_KEY; ключ не передаётся через аргументы или исходный код.");
            }

            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = new Uri(options.GeminiBaseUrl),
            };
            var client = new OpenAIClient(
                new System.ClientModel.ApiKeyCredential(options.GeminiApiKey),
                clientOptions);
            return new GeminiCompatibleChatClient(client.GetChatClient(options.GeminiModel).AsIChatClient());
        }

        if (options.ModelProvider.Equals("openrouter", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.OpenRouterApiKey))
            {
                throw new InvalidOperationException(
                    "MODEL_PROVIDER=openrouter требует OPENROUTER_API_KEY; ключ не передаётся через аргументы или исходный код.");
            }

            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = new Uri(options.OpenRouterBaseUrl),
            };
            var client = new OpenAIClient(
                new System.ClientModel.ApiKeyCredential(options.OpenRouterApiKey),
                clientOptions);
            return client.GetChatClient(options.OpenRouterModel).AsIChatClient();
        }

        if (!options.ModelProvider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Неизвестный MODEL_PROVIDER='{options.ModelProvider}'. Разрешены ollama, gemini и openrouter.");
        }

        return new OllamaApiClient(new Uri(options.OllamaHost), options.OllamaModel);
    }

    private static ArchitectureQuestion CreateQuestion(string[] args, AgentClientOptions options)
    {
        var prompt = args.Length > 0
            ? string.Join(' ', args)
            : "Как безопасно добавить новый MCP-инструмент в workspace?";

        var providerConstraint = options.ModelProvider.ToLowerInvariant() switch
        {
            "openrouter" => "использовать OpenRouter только через OPENROUTER_API_KEY и не раскрывать секрет",
            "gemini" => "использовать Gemini только через GEMINI_API_KEY и не раскрывать секрет",
            _ => "не использовать облачные модели",
        };
        var providerContext = options.ModelProvider.ToLowerInvariant() switch
        {
            "openrouter" => $"OpenRouter model={options.OpenRouterModel}",
            "gemini" => $"Gemini model={options.GeminiModel}",
            _ => "локальным Ollama",
        };

        return new ArchitectureQuestion(
            prompt,
            $"Минимальный workspace на .NET с MCP Server, AgentClient, {providerContext} и Docker Compose.",
            [providerConstraint, "сохранить русскоязычную документацию", "не нарушить MCP stdio transport"]);
    }
}

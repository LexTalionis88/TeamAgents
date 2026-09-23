using System.Diagnostics;
using AgentClient.Agents;
using AgentClient.Infrastructure.Ai.Abstractions;
using AgentClient.Infrastructure.Mcp;
using AgentClient.Infrastructure.Observability;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Specialized.Magentic;
using Microsoft.Extensions.AI;

namespace AgentClient.Workflow;

#pragma warning disable MAAIW001
#pragma warning disable MAAI001

/// <summary>
/// Magentic-адаптер над текущими ролями. Динамическая маршрутизация и replanning
/// переданы Agent Framework, а workspace evidence и финальный gate остаются типизированными.
/// </summary>
internal sealed class MagenticWorkflow(
    AgentSet agents,
    WorkflowRunMetadata metadata,
    ActivitySource activitySource,
    IChatClientProvider chatClientProvider,
    int maxCycles,
    int maxWorkItems,
    int agentTimeoutSeconds,
    CancellationToken workflowCancellationToken,
    bool readOnly) : IWorkflowRunner
{
    public async Task<ReviewResult> RunAsync(ArchitectureQuestion initialQuestion)
    {
        var manager = agents.MagenticManager
            ?? throw new InvalidOperationException("MagenticManager is not configured.");

        using var activity = activitySource.StartActivity("magentic.workflow", ActivityKind.Internal);
        metadata.Apply(activity, "MagenticManager", "magentic", 1);
        activity?.SetTag("workflow.orchestration", "magentic");
        activity?.SetTag("workflow.max_cycles", maxCycles);
        activity?.SetTag("workflow.max_work_items", maxWorkItems);

        var participants = new List<Microsoft.Agents.AI.AIAgent>
        {
            agents.Architect,
            agents.Developer,
            agents.Tester,
            agents.SecurityReviewer,
            agents.Reviewer,
        };

        var workflow = new MagenticWorkflowBuilder(manager)
            .AddParticipants(participants)
            .WithName("Workspace Magentic Orchestration")
            .WithDescription("Coordinates workspace implementation, verification, security and review agents.")
            .RequirePlanSignoff(false)
            .WithMaxRounds(CalculateMaxRounds())
            .WithMaxStalls(Math.Max(1, maxCycles))
            .WithMaxResets(Math.Max(0, maxCycles))
            .WithResponseLanguage("Russian")
            .Build();

        var task = new ChatMessage(
            ChatRole.User,
            $"Задача пользователя:\n{initialQuestion.Question}\n\n" +
            $"Контекст:\n{initialQuestion.Context}\n\n" +
            $"Ограничения:\n{string.Join("\n", initialQuestion.Constraints.Select(value => $"- {value}"))}\n\n" +
            "Работайте только в рамках этой задачи. Developer может изменять workspace через MCP. " +
            "Фактическим доказательством считаются только результаты MCP и проверок.");

        List<ChatMessage>? transcript = null;
        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow,
            new List<ChatMessage> { task },
            cancellationToken: workflowCancellationToken);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (var workflowEvent in run.WatchStreamAsync(workflowCancellationToken))
        {
            switch (workflowEvent)
            {
                case MagenticPlanCreatedEvent planCreated:
                    Console.WriteLine($"[MAGENTIC] План создан: {planCreated.FullTaskLedger.Text}");
                    break;
                case MagenticReplannedEvent replanned:
                    Console.WriteLine($"[MAGENTIC] План пересмотрен: {replanned.FullTaskLedger.Text}");
                    break;
                case MagenticProgressLedgerUpdatedEvent progress:
                    Console.WriteLine(
                        $"[MAGENTIC] Прогресс: satisfied={progress.ProgressLedger.IsRequestSatisfied}; " +
                        $"progressing={progress.ProgressLedger.IsProgressBeingMade}; " +
                        $"next={progress.ProgressLedger.NextSpeaker}");
                    break;
                case WorkflowOutputEvent output when output.Is<List<ChatMessage>>():
                    transcript = output.As<List<ChatMessage>>();
                    break;
                case WorkflowErrorEvent error:
                    throw error.Exception ?? new InvalidOperationException("Magentic workflow failed.");
            }
        }

        if (transcript is null)
        {
            throw new InvalidOperationException("Magentic workflow finished without a transcript.");
        }

        var mcp = new WorkspaceMcpInvoker(
            agents.Tools ?? Array.Empty<AITool>(),
            activitySource,
            metadata,
            workflowCancellationToken);
        var workspaceDiff = await mcp.InvokeAsync(
            "get_workspace_diff",
            new Dictionary<string, object?>(),
            "Reviewer",
            "magentic-final-diff",
            1);
        var workspaceEvidence = await mcp.GetEvidenceAsync(
            "Reviewer",
            "magentic-final-evidence",
            1);
        var checks = readOnly
            ? "READ_ONLY: checks were not executed."
            : await mcp.InvokeAsync(
                "run_dotnet_check",
                new Dictionary<string, object?>
                {
                    ["command"] = "dotnet test workspace.slnx --no-restore",
                },
                "Tester",
                "magentic-final-test",
                1);

        var transcriptText = string.Join(
            "\n\n",
            transcript.Select(message =>
                $"{message.AuthorName ?? message.Role.ToString()}: {message.Text}"));

        return await TypedAgentRunner.RunAsync<ReviewResult>(
            agents.Reviewer,
            new
            {
                Requirements = initialQuestion,
                MagenticTranscript = transcriptText,
                WorkspaceDiff = workspaceDiff,
                WorkspaceEvidence = workspaceEvidence,
                Verification = checks,
            },
            metadata,
            "Reviewer",
            "magentic-final-review",
            1,
            chatClientProvider.RequiresLocalTypedJson,
            agentTimeoutSeconds,
            workflowCancellationToken);
    }

    private int CalculateMaxRounds()
    {
        var estimatedRounds = Math.Max(1, maxCycles) * Math.Max(1, maxWorkItems) * 4;
        return Math.Clamp(estimatedRounds, 4, 48);
    }
}

#pragma warning restore MAAI001
#pragma warning restore MAAIW001

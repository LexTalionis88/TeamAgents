using System.Diagnostics;
using System.Text.Json;
using AgentClient.Agents;
using AgentClient.Infrastructure.Ai.Abstractions;
using AgentClient.Infrastructure.Mcp;
using AgentClient.Infrastructure.Observability;
using Microsoft.Extensions.AI;

namespace AgentClient.Workflow;

internal sealed class EscalatingWorkflow(
    AgentSet agents,
    WorkflowRunMetadata metadata,
    ActivitySource activitySource,
    IChatClientProvider chatClientProvider,
    int maxCycles,
    int maxWorkItems,
    int agentTimeoutSeconds,
    CancellationToken workflowCancellationToken,
    bool readOnly)
{
    /// <summary>
    /// Выполняет общий workflow декомпозиции, реализации, проверок и bounded-эскалаций.
    /// </summary>
    /// <param name="initialQuestion">Исходные требования пользователя.</param>
    public async Task<ReviewResult> RunAsync(ArchitectureQuestion initialQuestion)
    {
        if (readOnly)
        {
            return await RunReadOnlyAsync(initialQuestion);
        }

        // Workflow отвечает за маршрутизацию и evidence-gates. Сама задача является
        // данными пользователя; предметная реализация здесь не выбирается.
        var question = initialQuestion;
        var minimumWorkItems = TaskPlanValidator.GetMinimumWorkItems(question.Question, maxWorkItems);
        var taskPlan = await RunAsync<TaskPlan>(
            agents.Manager,
            new
            {
                Requirements = question,
                PlanLimits = new
                {
                    MinimumWorkItems = minimumWorkItems,
                    MaximumWorkItems = maxWorkItems,
                },
            },
            "Manager",
            "decomposition",
            1);
        taskPlan = await EnsureValidTaskPlanAsync(question, taskPlan, maxWorkItems, minimumWorkItems);

        var intakeMcp = CreateMcpInvoker();
        var workspaceStatus = await intakeMcp.InvokeAsync(
            "get_workspace_status",
            new Dictionary<string, object?>(),
            "Architect",
            "architecture-mcp-status",
            1);
        var architecture = await RunAsync<ArchitectureDecision>(
            agents.Architect,
            question with
            {
                Context = $"{question.Context}\nManager task plan summary: {taskPlan.Summary}\n" +
                    string.Join("\n", taskPlan.Items.Select(item =>
                        $"- {item.Id}: {item.Title}. Objective: {item.Objective}. Dependencies: {string.Join(", ", item.Dependencies)}")) +
                    $"\nMCP workspace status evidence:\n{workspaceStatus}"
            },
            "Architect",
            "architecture",
            1);
        architecture = await EnsureValidArchitectureAsync(question, architecture, 1);

        var totalWorkItems = Math.Min(taskPlan.Items.Count, maxWorkItems);
        for (var workItemIndex = 0; workItemIndex < totalWorkItems; workItemIndex++)
        {
            var currentWorkItem = taskPlan.Items[workItemIndex];
            TestReport? pendingTesterReport = null;
            ReviewResult? pendingReviewerReport = null;
            ImplementationResult? previousImplementation = null;
            var workItemCompleted = false;

            for (var cycle = 1; cycle <= maxCycles; cycle++)
            {
                object implementationInput = pendingReviewerReport is not null
                    ? new DeveloperReviewerFixRequest(
                        architecture,
                        previousImplementation ?? throw new InvalidOperationException(
                            "Нельзя применить замечания Reviewer без предыдущего ImplementationResult."),
                        pendingReviewerReport,
                        cycle)
                    : pendingTesterReport is null
                        ? architecture
                        : new DeveloperTesterFixRequest(
                            architecture,
                            previousImplementation ?? throw new InvalidOperationException(
                            "Нельзя применить замечания Tester без предыдущего ImplementationResult."),
                            pendingTesterReport,
                            cycle);

                var implementation = await RunDeveloperAsync(
                    new
                    {
                        Requirements = question,
                        PlanSummary = taskPlan.Summary,
                        CurrentWorkItem = currentWorkItem,
                        Request = implementationInput,
                    },
                    cycle,
                    "implementation");
                implementation = await EnsureValidImplementationAsync(
                    question,
                    architecture,
                    taskPlan,
                    currentWorkItem,
                    implementation,
                    cycle);
                previousImplementation = implementation;
                pendingTesterReport = null;
                pendingReviewerReport = null;

                var developerDecision = await DecideAsync(
                    new
                    {
                        Input = new DeveloperEscalationInput(architecture, implementation, cycle),
                        PlanSummary = taskPlan.Summary,
                        CurrentWorkItem = currentWorkItem,
                    },
                    cycle);
                developerDecision = NormalizeDeveloperDecision(developerDecision, implementation, cycle);
                if (developerDecision.NextAgent.Equals("Architect", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureCycleAvailable(cycle, "Developer запросил уточнение архитектуры");
                    question = implementation.ArchitectureQuestion
                        ?? throw new InvalidOperationException(
                            "Developer выбрал эскалацию, но не вернул ArchitectureQuestion.");
                    TraceTransition("Developer", "Architect", cycle, developerDecision.Reason);
                    architecture = await RunAsync<ArchitectureDecision>(
                        agents.Architect, question, "Architect", "architecture", cycle + 1);
                    architecture = await EnsureValidArchitectureAsync(question, architecture, cycle + 1);
                    continue;
                }

                if (developerDecision.NextAgent.Equals("Developer", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureCycleAvailable(cycle, "Manager запросил ещё одну итерацию Developer");
                    TraceTransition("Developer", "Developer", cycle, developerDecision.Reason);
                    continue;
                }

                EnsureOneOf(developerDecision, "Developer", "Developer", "Architect", "Tester");
                EnsureRoute(developerDecision, "Tester", "Developer");
                TraceTransition("Developer", "Tester", cycle, "Реализация Developer передана на проверку");

            var testerMcp = CreateMcpInvoker();
            var testerDiff = await testerMcp.InvokeAsync(
                "get_workspace_diff", new Dictionary<string, object?>(), "Tester", "testing-diff", cycle);
            var testerCheck = await testerMcp.InvokeAsync(
                "run_dotnet_check",
                new Dictionary<string, object?> { ["command"] = "dotnet test workspace.slnx --no-restore" },
                "Tester", "testing-dotnet", cycle);
            var testerEvidence = await testerMcp.GetEvidenceAsync("Tester", "testing-evidence", cycle);
            var tests = await RunAsync<TestReport>(
                agents.Tester,
                new
                {
                    PlanSummary = taskPlan.Summary,
                    CurrentWorkItem = currentWorkItem,
                    Implementation = implementation,
                    WorkspaceDiff = testerDiff,
                    DotnetTest = testerCheck,
                    WorkspaceEvidence = testerEvidence,
                },
                "Tester", "testing", cycle);
            var testDecision = await DecideAsync(
                new
                {
                    Input = new TestEscalationInput(implementation, tests, cycle),
                    PlanSummary = taskPlan.Summary,
                    CurrentWorkItem = currentWorkItem,
                },
                cycle);
            testDecision = NormalizeTesterDecision(testDecision, tests, cycle);

            if (testDecision.NextAgent.Equals("Developer", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCycleAvailable(cycle, "Tester вернул замечания для следующей итерации реализации");
                pendingTesterReport = tests;
                TraceTransition("Tester", "Developer", cycle, testDecision.Reason);
                continue;
            }

            EnsureRoute(testDecision, "Security", "Tester");
            var securityMcp = CreateMcpInvoker();
            var securityDiff = await securityMcp.InvokeAsync(
                "get_workspace_diff", new Dictionary<string, object?>(), "Security", "security-diff", cycle);
            var securityEvidence = await securityMcp.GetEvidenceAsync("Security", "security-evidence", cycle);
            var security = await RunAsync<SecurityReview>(
                agents.SecurityReviewer,
                new
                {
                    PlanSummary = taskPlan.Summary,
                    CurrentWorkItem = currentWorkItem,
                    Input = new SecurityReviewInput(architecture, tests, cycle),
                    WorkspaceDiff = securityDiff,
                    WorkspaceEvidence = securityEvidence,
                },
                "Security", "security", cycle);
            var securityDecision = await DecideAsync(
                new
                {
                    Input = new SecurityEscalationInput(architecture, tests, security, cycle),
                    PlanSummary = taskPlan.Summary,
                    CurrentWorkItem = currentWorkItem,
                },
                cycle);

            if (securityDecision.NextAgent.Equals("Architect", StringComparison.OrdinalIgnoreCase) &&
                !security.ArchitectureChallenged)
            {
                TraceTransition("Manager", "Reviewer", cycle,
                    "Typed gate исправил недопустимый маршрут после Security без ArchitectureChallenged=true");
                securityDecision = securityDecision with
                {
                    NextAgent = "Reviewer",
                    Reason = "Typed gate: Security не оспорил архитектуру; маршрут изменён на Reviewer."
                };
            }

            if (securityDecision.NextAgent.Equals("Architect", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCycleAvailable(cycle, "Security challenged the architecture");
                TraceTransition("Security", "Architect", cycle, securityDecision.Reason);
                architecture = await RunAsync<ArchitectureDecision>(
                    agents.Architect,
                    new ArchitectureRevisionRequest(architecture, security, cycle),
                    "Architect", "architecture", cycle + 1);
                architecture = await EnsureValidArchitectureAsync(question, architecture, cycle + 1);
                continue;
            }

            if (securityDecision.NextAgent.Equals("Developer", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCycleAvailable(cycle, "Security вернул замечания для следующей итерации реализации");
                TraceTransition("Security", "Developer", cycle, securityDecision.Reason);
                continue;
            }

            if (!securityDecision.NextAgent.Equals("Reviewer", StringComparison.OrdinalIgnoreCase))
            {
                TraceTransition("Manager", "Reviewer", cycle,
                    $"Typed gate исправил недопустимый маршрут после Security: {securityDecision.NextAgent}");
                securityDecision = securityDecision with
                {
                    NextAgent = "Reviewer",
                    Reason = "Typed gate: после Security разрешён Reviewer, если нет эскалации к Architect или Developer."
                };
            }

            EnsureRoute(securityDecision, "Reviewer", "Security");
            var reviewerMcp = CreateMcpInvoker();
            var reviewerDiff = await reviewerMcp.InvokeAsync(
                "get_workspace_diff", new Dictionary<string, object?>(), "Reviewer", "review-diff", cycle);
            var reviewerEvidence = await reviewerMcp.GetEvidenceAsync("Reviewer", "review-evidence", cycle);
            var reviewInput = new ReviewerInput(architecture, implementation, tests, security, cycle);
            var review = await RunAsync<ReviewResult>(
                agents.Reviewer,
                new
                {
                    PlanSummary = taskPlan.Summary,
                    CurrentWorkItem = currentWorkItem,
                    Input = reviewInput,
                    WorkspaceDiff = reviewerDiff,
                    WorkspaceEvidence = reviewerEvidence,
                },
                "Reviewer", "review", cycle);
            var reviewDecision = await DecideAsync(
                new
                {
                    Input = new ReviewEscalationInput(reviewInput, review, cycle),
                    PlanSummary = taskPlan.Summary,
                    CurrentWorkItem = currentWorkItem,
                },
                cycle);
            reviewDecision = NormalizeReviewDecision(reviewDecision, review, cycle);
            if (reviewDecision.NextAgent.Equals("Developer", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCycleAvailable(cycle, "Reviewer вернул блокирующие замечания для следующей итерации реализации");
                pendingReviewerReport = review;
                TraceTransition("Reviewer", "Developer", cycle, reviewDecision.Reason);
                continue;
            }

            EnsureRoute(reviewDecision, "Manager", "Reviewer");
            if (workItemIndex + 1 < totalWorkItems)
            {
                workItemCompleted = true;
                TraceTransition("Reviewer", "Manager", cycle,
                    $"WorkItem {currentWorkItem.Id} принят Manager; план продолжится следующим срезом.");
                TraceTransition("Manager", "Developer", cycle,
                    $"Переход к WorkItem {taskPlan.Items[workItemIndex + 1].Id}.");
                break;
            }

            TraceTransition("Reviewer", "Manager", cycle, reviewDecision.Reason);
            return await RunAsync<ReviewResult>(
                agents.FinalManager, review, "Manager", "manager-final", cycle);
        }

            if (!workItemCompleted)
            {
                throw new InvalidOperationException(
                    $"WorkItem {currentWorkItem.Id} не завершён за WORKFLOW_MAX_CYCLES={maxCycles}.");
            }
        }

        throw new InvalidOperationException("План декомпозиции не завершён в установленном лимите work items.");
    }

    private WorkspaceMcpInvoker CreateMcpInvoker() =>
        new(agents.Tools ?? Array.Empty<AITool>(), activitySource, metadata, workflowCancellationToken);

    private async Task<ReviewResult> RunReadOnlyAsync(ArchitectureQuestion question)
    {
        var mcp = CreateMcpInvoker();
        var status = await mcp.InvokeAsync(
            "get_workspace_status",
            new Dictionary<string, object?>(),
            "ReadOnly",
            "read-only-status",
            1);
        var diff = await mcp.InvokeAsync(
            "get_workspace_diff",
            new Dictionary<string, object?>(),
            "ReadOnly",
            "read-only-diff",
            1);
        var evidence = await mcp.GetEvidenceAsync("ReadOnly", "read-only-evidence", 1);

        return await RunAsync<ReviewResult>(
            agents.Reviewer,
            new
            {
                Question = question,
                ReadOnly = true,
                WorkspaceStatus = status,
                WorkspaceDiff = diff,
                WorkspaceEvidence = evidence,
                Rule = "Не предлагай и не заявляй изменения как выполненные; верни только наблюдаемое состояние и безопасные следующие шаги.",
            },
            "Reviewer",
            "read-only-review",
            1);
    }

    private async Task<ManagerDecision> DecideAsync(object input, int cycle) =>
        await RunAsync<ManagerDecision>(agents.PolicyManager, input, "Manager", "manager-decision", cycle);

    private ManagerDecision NormalizeDeveloperDecision(
        ManagerDecision decision,
        ImplementationResult implementation,
        int cycle)
    {
        var expected = implementation.NeedsClarification
            ? "Architect"
            : implementation.Implemented
                ? "Tester"
                : "Developer";

        if (decision.NextAgent.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            return decision;
        }

        TraceTransition("Manager", expected, cycle,
            $"Typed gate исправил недопустимый маршрут Manager: {decision.NextAgent}");
        return decision with
        {
            NextAgent = expected,
            Reason = $"Typed gate: маршрут {decision.NextAgent} несовместим с состоянием Developer; выбран {expected}."
        };
    }

    private ManagerDecision NormalizeTesterDecision(
        ManagerDecision decision,
        TestReport tests,
        int cycle)
    {
        var expected = tests.Passed && !tests.RequiresEscalation ? "Security" : "Developer";
        if (decision.NextAgent.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            return decision;
        }

        TraceTransition("Manager", expected, cycle,
            $"Typed gate исправил недопустимый маршрут Tester: {decision.NextAgent}");
        return decision with
        {
            NextAgent = expected,
            Reason = $"Typed gate: TestReport не разрешает маршрут {decision.NextAgent}; выбран {expected}."
        };
    }

    private ManagerDecision NormalizeReviewDecision(
        ManagerDecision decision,
        ReviewResult review,
        int cycle)
    {
        var blocking = !review.Approved || review.BlockingIssues.Count > 0;
        var expected = blocking ? "Developer" : "Manager";
        if (decision.NextAgent.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            return decision;
        }

        TraceTransition("Manager", expected, cycle,
            $"Typed gate исправил недопустимый маршрут Reviewer: {decision.NextAgent}");
        return decision with
        {
            NextAgent = expected,
            Reason = blocking
                ? $"Typed gate: блокирующие замечания Reviewer требуют Developer; маршрут {decision.NextAgent} отклонён."
                : $"Typed gate: Reviewer одобрил результат; Manager принимает маршрут {decision.NextAgent}."
        };
    }

    private async Task<ImplementationResult> RunDeveloperAsync(object input, int cycle, string step)
    {
        var exploration = await TypedAgentRunner.RunActionAsync(
            agents.DeveloperExplorer ?? agents.Developer,
            input,
            metadata,
            "Developer",
            $"{step}-explore",
            cycle,
            "Работай только на этапе Explore. Проверь через MCP AGENTS.md, относящуюся к задаче документацию репозитория и исходные файлы. Не изменяй workspace. Верни краткий ExplorationReport с применимыми компонентами, инвариантами и файлами.",
            180);

        var implementationAction = await TypedAgentRunner.RunActionAsync(
            agents.DeveloperImplementer ?? agents.Developer,
            new { Request = input, ExplorationReport = exploration },
            metadata,
            "Developer",
            $"{step}-implement",
            cycle,
            "Работай только на этапе Execute. Во входе есть PlanSummary и CurrentWorkItem: реализуй только текущий срез, его AcceptanceCriteria и необходимые Dependencies, не пытайся за один этап реализовать весь план. Не запрашивай уточнения и не возвращай NeedsClarification для исходной задачи: если solution не содержит предметного проекта, добавь новый проект в текущую solution, сохранив AgentClient и MCP Server, и выбери минимальную рабочую реализацию. Сначала изучи нужные файлы, затем создай или измени необходимые для текущего среза проекты, исходный код, конфигурацию, Docker и тесты через MCP. Не завершай этап без хотя бы одного успешного MCP-инструмента изменения, если текущий срез требует реализации. После изменений запусти доступные проверки. Не возвращай patch вместо действий и не изменяй MCP Server, AgentClient, workflow или typed contracts, если это не требуется напрямую исходной задачей. Все новые комментарии и документация должны быть на русском. Заверши кратким отчётом только о реально выполненных MCP-действиях.",
            300);

        var mcp = CreateMcpInvoker();
        var implementationPatches = ParseImplementationPatches(implementationAction);
        var patchResults = new List<string>(implementationPatches.Count);
        for (var patchIndex = 0; patchIndex < implementationPatches.Count; patchIndex++)
        {
            var patch = implementationPatches[patchIndex];
            var patchResult = await mcp.ApplyPatchAsync(
                patch.Patch,
                "Developer",
                $"{step}-apply-{patchIndex + 1}",
                cycle);
            patchResults.Add($"Patch {patchIndex + 1} ({patch.Summary}): {patchResult}");
            if (!patchResult.StartsWith("PATCH_APPLIED", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Developer patch {patchIndex + 1} не применён: {patchResult}");
            }
        }

        var workspaceDiff = await mcp.InvokeAsync(
            "get_workspace_diff", new Dictionary<string, object?>(), "Developer", $"{step}-diff", cycle);
        var restoreCheck = await mcp.InvokeAsync(
            "run_dotnet_check",
            new Dictionary<string, object?> { ["command"] = "dotnet restore workspace.slnx" },
            "Developer", $"{step}-restore", cycle);
        var buildCheck = await mcp.InvokeAsync(
            "run_dotnet_check",
            new Dictionary<string, object?> { ["command"] = "dotnet build workspace.slnx --no-restore" },
            "Developer", $"{step}-build", cycle);
        var testCheck = await mcp.InvokeAsync(
            "run_dotnet_check",
            new Dictionary<string, object?> { ["command"] = "dotnet test workspace.slnx --no-restore" },
            "Developer", $"{step}-test", cycle);
        var evidence = await mcp.GetEvidenceAsync("Developer", $"{step}-evidence", cycle);

        var patchTranscript = string.Join(Environment.NewLine, patchResults);

        var verification = await TypedAgentRunner.RunActionAsync(
            agents.DeveloperVerifier ?? agents.Developer,
            new
            {
                Request = input,
                ActionTranscript = implementationAction,
                PatchApplication = patchTranscript,
                WorkspaceDiff = workspaceDiff,
                RestoreCheck = restoreCheck,
                BuildCheck = buildCheck,
                TestCheck = testCheck,
                WorkspaceEvidence = evidence,
            },
            metadata,
            "Developer",
            $"{step}-verify",
            cycle,
            "Проверяй только переданные MCP diff, результаты команд и workspace evidence. Не изменяй workspace и не выдумывай факты. Workflow уже выполнил MCP-проверки; не пытайся повторять их в этом ответе. Верни краткий VerificationReport.",
            180);
        var actionTranscript =
            $"DeveloperAction:\n{implementationAction}\n\nPatchApplication:\n{patchTranscript}\n\nVerification:\n{verification}\n\nWorkspaceEvidence:\n{evidence}";

        var implementation = await RunAsync<ImplementationResult>(
            agents.DeveloperResultFormatter,
            new
            {
                Input = input,
                ActionTranscript = actionTranscript,
                EvidenceRule = "Устанавливай Implemented=true только когда WorkspaceEvidence подтверждает реальные изменённые файлы и DeveloperAction/Verification не противоречат этому. Копируй ChangedFiles, WorkspaceRevision, DiffHash и ToolCalls только из наблюдаемого evidence и transcript.",
            },
            "Developer",
            $"{step}-result",
            cycle);

        if (implementation.Implemented &&
            implementation.ToolCalls is not { Count: > 0 } &&
            implementation.ChangedFiles is { Count: > 0 })
        {
            implementation = implementation with
            {
                ToolCalls = [
                    patchResults.Count > 0 || implementationAction.Contains("apply_workspace_patch", StringComparison.OrdinalIgnoreCase)
                        ? "apply_workspace_patch"
                        : "replace_workspace_file"
                ],
            };
        }

        return implementation;
    }

    private async Task<TaskPlan> EnsureValidTaskPlanAsync(
        ArchitectureQuestion requirements,
        TaskPlan taskPlan,
        int maximumItems,
        int minimumItems)
    {
        if (TaskPlanValidator.IsValid(taskPlan, maximumItems, minimumItems))
        {
            return taskPlan;
        }

        var corrected = await RunAsync<TaskPlan>(
            agents.Manager,
            new
            {
                Requirements = requirements,
                InvalidPlan = taskPlan,
                Correction = $"Верни TaskPlan с {minimumItems}–{maximumItems} последовательными WorkItem. Каждый WorkItem обязан иметь уникальный Id, непустые Title и Objective, минимум один AcceptanceCriteria и массив Dependencies только на предыдущие WorkItem. Не объединяй в один WorkItem разные группы изменений; раздели API, данные, инфраструктуру, тесты и документацию по связным срезам. Не изменяй исходные требования и не запрашивай уточнений.",
            },
            "Manager",
            "decomposition-correction",
            1);

        if (!TaskPlanValidator.IsValid(corrected, maximumItems, minimumItems))
        {
            throw new InvalidOperationException(
                $"Manager вернул некорректный TaskPlan: требуется от {minimumItems} до {maximumItems} последовательных WorkItem с критериями приёмки.");
        }

        return corrected;
    }

    private async Task<ImplementationResult> EnsureValidImplementationAsync(
        ArchitectureQuestion requirements,
        ArchitectureDecision architecture,
        TaskPlan taskPlan,
        WorkItem currentWorkItem,
        ImplementationResult implementation,
        int cycle)
    {
        if (IsValidImplementation(implementation))
        {
            return implementation;
        }

        EnsureCycleAvailable(cycle, "Developer не предоставил ImplementationResult с подтверждённым evidence");
        TraceTransition("Developer", "Developer", cycle,
            "Governance вернул workflow к Developer для исправления с подтверждённым evidence");
        var corrected = await RunDeveloperAsync(
            new
            {
                Requirements = requirements,
                PlanSummary = taskPlan.Summary,
                CurrentWorkItem = currentWorkItem,
                Request = new ImplementationCorrectionRequest(
                    architecture,
                    implementation,
                    "Используй реальные изменения workspace через MCP и результаты команд/evidence; ChangedFiles должны подтверждаться workspace diff.",
                    cycle),
            },
            cycle,
            "implementation-correction");
        ValidateImplementation(corrected);
        return corrected;
    }

    private async Task<ArchitectureDecision> EnsureValidArchitectureAsync(
        ArchitectureQuestion question,
        ArchitectureDecision architecture,
        int cycle)
    {
        if (architecture.RequirementsAccepted && architecture.ChangedRequirements is { Count: > 0 })
        {
            // Architect не может самостоятельно одобрить изменения требований.
            // Такие изменения должны пройти явный policy-gate Manager.
            TraceTransition("Architect", "Manager", cycle,
                "Governance отбросил неподтверждённые ChangedRequirements");
            architecture = architecture with { ChangedRequirements = Array.Empty<string>() };
        }

        if (IsValidArchitecture(architecture))
        {
            return architecture;
        }

        EnsureCycleAvailable(cycle, "Architect вернул решение с неподтверждёнными изменениями требований");
        TraceTransition("Architect", "Architect", cycle,
            "Governance запросил исправление контракта архитектуры");
        var corrected = await RunAsync<ArchitectureDecision>(
            agents.Architect,
            new
            {
                Requirements = question,
                Correction = new ArchitectureCorrectionRequest(
                    architecture,
                    "Верни все поля ArchitectureDecision с точными именами контракта. Summary не должен быть пустым, Decisions и AffectedAreas должны содержать минимум по одному конкретному пункту, RequirementsAccepted должен быть true, ChangedRequirements — пустым; не добавляй требования, которых нет во входе. Все текстовые значения пиши на русском.",
                    cycle),
            },
            "Architect",
            "architecture-correction",
            cycle);
        ValidateArchitecture(corrected);
        return corrected;
    }

    private async ValueTask<T> RunAsync<T>(
        Microsoft.Agents.AI.AIAgent agent,
        object input,
        string agentName,
        string step,
        int iteration) =>
        await TypedAgentRunner.RunAsync<T>(
            agent,
            input,
            metadata,
            agentName,
            step,
            iteration,
            chatClientProvider.RequiresLocalTypedJson,
            agentTimeoutSeconds,
            workflowCancellationToken);

    private void TraceTransition(string from, string to, int cycle, string reason)
    {
        using var activity = activitySource.StartActivity("workflow.transition");
        metadata.Apply(activity, "Manager", $"transition:{from}->{to}", cycle);
        activity?.SetTag("from.agent", from);
        activity?.SetTag("to.agent", to);
        activity?.SetTag("manager.reason", reason);
    }

    private void EnsureCycleAvailable(int cycle, string reason)
    {
        if (cycle >= maxCycles)
        {
            throw new InvalidOperationException(
                $"Workflow остановлен: {reason}. Достигнут WORKFLOW_MAX_CYCLES={maxCycles}.");
        }
    }

    private static void EnsureRoute(ManagerDecision decision, string expected, string source)
    {
        if (!decision.NextAgent.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Manager выбрал недопустимый маршрут после {source}: {decision.NextAgent}; ожидался {expected}.");
        }
    }

    private static void EnsureOneOf(ManagerDecision decision, string source, params string[] allowed)
    {
        if (!allowed.Contains(decision.NextAgent, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Manager выбрал недопустимый маршрут после {source}: {decision.NextAgent}; " +
                $"разрешены: {string.Join(", ", allowed)}.");
        }
    }

    private static void ValidateImplementation(ImplementationResult implementation)
    {
        if ((implementation.RequirementsChanged || implementation.ArchitectureChanged) &&
            (!implementation.NeedsClarification || implementation.ArchitectureQuestion is null))
        {
            throw new InvalidOperationException(
                "Developer изменил требования или архитектуру, не вернув ArchitectureQuestion.");
        }

        if (!implementation.NeedsClarification &&
            (!implementation.Implemented || implementation.ChangedFiles is null || implementation.ChangedFiles.Count == 0 ||
             string.IsNullOrWhiteSpace(implementation.WorkspaceRevision) ||
             string.IsNullOrWhiteSpace(implementation.DiffHash) ||
             implementation.ToolCalls is null || implementation.ToolCalls.Count == 0))
        {
            throw new InvalidOperationException(
                "Developer заявил о завершении без необходимого workspace evidence.");
        }
    }

    private static bool IsValidImplementation(ImplementationResult implementation) =>
        implementation.NeedsClarification ||
        (implementation.Implemented && implementation.ChangedFiles is { Count: > 0 } &&
         !string.IsNullOrWhiteSpace(implementation.DiffSummary) &&
         !string.IsNullOrWhiteSpace(implementation.WorkspaceRevision) &&
         !string.IsNullOrWhiteSpace(implementation.DiffHash) &&
         implementation.ToolCalls is { Count: > 0 });

    private static void ValidateArchitecture(ArchitectureDecision architecture)
    {
        if (!IsValidArchitecture(architecture))
        {
            throw new InvalidOperationException(
                "Architect изменил или не принял требования без approval Manager.");
        }
    }

    private static bool IsValidArchitecture(ArchitectureDecision architecture) =>
        architecture.RequirementsAccepted &&
        architecture.ChangedRequirements is { Count: 0 } &&
        !string.IsNullOrWhiteSpace(architecture.Summary) &&
        architecture.Decisions is { Count: > 0 } &&
        architecture.AffectedAreas is { Count: > 0 };

    private static IReadOnlyList<ImplementationPatch> ParseImplementationPatches(string response)
    {
        var content = response.Trim();
        if (content.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = content.IndexOf('\n');
            var closingFence = content.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && closingFence > firstNewLine)
            {
                content = content[(firstNewLine + 1)..closingFence].Trim();
            }
        }

        var patches = new List<ImplementationPatch>();
        foreach (var candidate in ExtractJsonObjects(content))
        {
            try
            {
                var patch = JsonSerializer.Deserialize<ImplementationPatch>(candidate, JsonSerializerOptions.Web);
                if (patch is not null && !string.IsNullOrWhiteSpace(patch.Patch))
                {
                    patches.Add(patch);
                }
            }
            catch (JsonException)
            {
                // Обрезанный последний объект не должен скрывать завершённые patch-объекты.
            }
        }

        if (patches.Count == 0)
        {
            foreach (var rawPatch in ExtractQuotedPropertyValues(content, "Patch"))
            {
                var decoded = DecodeJsonString(rawPatch);
                if (!string.IsNullOrWhiteSpace(decoded))
                {
                    patches.Add(new ImplementationPatch(decoded, "Patch извлечён из завершённого ответа модели."));
                }
            }
        }

        if (patches.Count == 0)
        {
            // Developer может применить изменение напрямую через MCP и вернуть
            // обычный action-отчёт; в этом случае evidence проверяется ниже.
            return Array.Empty<ImplementationPatch>();
        }

        return patches;
    }

    private static IEnumerable<string> ExtractQuotedPropertyValues(string content, string propertyName)
    {
        var marker = $"\"{propertyName}\"";
        var offset = 0;
        while ((offset = content.IndexOf(marker, offset, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var colon = content.IndexOf(':', offset + marker.Length);
            if (colon < 0)
            {
                yield break;
            }

            var quote = content.IndexOf('"', colon + 1);
            if (quote < 0)
            {
                yield break;
            }

            var end = quote + 1;
            var escaped = false;
            for (; end < content.Length; end++)
            {
                var character = content[end];
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    break;
                }
            }

            if (end >= content.Length)
            {
                yield break;
            }

            yield return content[(quote + 1)..end];
            offset = end + 1;
        }
    }

    private static string DecodeJsonString(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<string>($"\"{value}\"") ?? string.Empty;
        }
        catch (JsonException)
        {
            return value
                .Replace("\\r", "\r", StringComparison.Ordinal)
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> ExtractJsonObjects(string content)
    {
        var depth = 0;
        var start = -1;
        var inString = false;
        var escaped = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
            }
            else if (character == '{')
            {
                if (depth == 0)
                {
                    start = index;
                }

                depth++;
            }
            else if (character == '}' && depth > 0)
            {
                depth--;
                if (depth == 0 && start >= 0)
                {
                    yield return content[start..(index + 1)];
                    start = -1;
                }
            }
        }
    }
}

using System.Diagnostics;
using AgentClient.Agents;
using AgentClient.Infrastructure.Observability;

namespace AgentClient.Workflow;

public sealed class EscalatingWorkflow(
    AgentSet agents,
    WorkflowRunMetadata metadata,
    ActivitySource activitySource,
    int maxCycles)
{
    public async Task<ReviewResult> RunAsync(ArchitectureQuestion initialQuestion)
    {
        var question = await RunAsync<ArchitectureQuestion>(
            agents.Manager, initialQuestion, "Manager", "manager-intake", 1);
        var architecture = await RunAsync<ArchitectureDecision>(
            agents.Architect, question, "Architect", "architecture", 1);
        architecture = await EnsureValidArchitectureAsync(architecture, 1);

        TestReport? pendingTesterReport = null;
        ReviewResult? pendingReviewerReport = null;
        ImplementationResult? previousImplementation = null;
        for (var cycle = 1; cycle <= maxCycles; cycle++)
        {
            object implementationInput = pendingReviewerReport is not null
                ? new DeveloperReviewerFixRequest(
                    architecture,
                    previousImplementation ?? throw new InvalidOperationException(
                        "Reviewer feedback cannot be applied without the previous ImplementationResult."),
                    pendingReviewerReport,
                    cycle)
                : pendingTesterReport is null
                    ? architecture
                    : new DeveloperTesterFixRequest(
                        architecture,
                        previousImplementation ?? throw new InvalidOperationException(
                            "Tester feedback cannot be applied without the previous ImplementationResult."),
                        pendingTesterReport,
                        cycle);
            var implementation = await RunAsync<ImplementationResult>(
                agents.Developer, implementationInput, "Developer", "implementation", cycle);
            implementation = await EnsureValidImplementationAsync(architecture, implementation, cycle);
            previousImplementation = implementation;
            pendingTesterReport = null;
            pendingReviewerReport = null;

            var developerDecision = await DecideAsync(
                new DeveloperEscalationInput(architecture, implementation, cycle), cycle);
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
                architecture = await EnsureValidArchitectureAsync(architecture, cycle + 1);
                continue;
            }

            if (developerDecision.NextAgent.Equals("Developer", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCycleAvailable(cycle, "Manager запросил повторный Developer");
                TraceTransition("Developer", "Developer", cycle, developerDecision.Reason);
                continue;
            }

            EnsureOneOf(developerDecision, "Developer", "Architect", "Tester");
            EnsureRoute(developerDecision, "Tester", "Developer");
            TraceTransition("Developer", "Tester", cycle,
                "Developer implementation submitted for verification");
            var tests = await RunAsync<TestReport>(
                agents.Tester, implementation, "Tester", "testing", cycle);
            var testDecision = await DecideAsync(
                new TestEscalationInput(implementation, tests, cycle), cycle);
            testDecision = NormalizeTesterDecision(testDecision, tests, cycle);

            if (testDecision.NextAgent.Equals("Developer", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCycleAvailable(cycle, "Tester вернул findings для повторной реализации");
                pendingTesterReport = tests;
                TraceTransition("Tester", "Developer", cycle, testDecision.Reason);
                continue;
            }

            EnsureRoute(testDecision, "Security", "Tester");
            var security = await RunAsync<SecurityReview>(
                agents.SecurityReviewer,
                new SecurityReviewInput(architecture, tests, cycle),
                "Security", "security", cycle);
            var securityDecision = await DecideAsync(
                new SecurityEscalationInput(architecture, tests, security, cycle), cycle);

            if (securityDecision.NextAgent.Equals("Architect", StringComparison.OrdinalIgnoreCase))
            {
                if (!security.ArchitectureChallenged)
                {
                    throw new InvalidOperationException(
                        "Manager выбрал Architect после Security без ArchitectureChallenged=true.");
                }

                EnsureCycleAvailable(cycle, "Security оспорил архитектурное решение");
                TraceTransition("Security", "Architect", cycle, securityDecision.Reason);
                architecture = await RunAsync<ArchitectureDecision>(
                    agents.Architect,
                    new ArchitectureRevisionRequest(architecture, security, cycle),
                    "Architect", "architecture", cycle + 1);
                architecture = await EnsureValidArchitectureAsync(architecture, cycle + 1);
                continue;
            }

            if (securityDecision.NextAgent.Equals("Developer", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCycleAvailable(cycle, "Security вернул findings для повторной реализации");
                TraceTransition("Security", "Developer", cycle, securityDecision.Reason);
                continue;
            }

            EnsureOneOf(securityDecision, "Security", "Architect", "Developer", "Reviewer");
            EnsureRoute(securityDecision, "Reviewer", "Security");
            var review = await RunAsync<ReviewResult>(
                agents.Reviewer,
                new ReviewerInput(architecture, implementation, tests, security, cycle),
                "Reviewer", "review", cycle);
            var reviewInput = new ReviewerInput(architecture, implementation, tests, security, cycle);
            var reviewDecision = await DecideAsync(
                new ReviewEscalationInput(reviewInput, review, cycle), cycle);
            reviewDecision = NormalizeReviewDecision(reviewDecision, review, cycle);
            if (reviewDecision.NextAgent.Equals("Developer", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCycleAvailable(cycle, "Reviewer вернул blocking findings для повторного Developer");
                pendingReviewerReport = review;
                TraceTransition("Reviewer", "Developer", cycle, reviewDecision.Reason);
                continue;
            }

            EnsureRoute(reviewDecision, "Manager", "Reviewer");
            TraceTransition("Reviewer", "Manager", cycle, reviewDecision.Reason);
            return await RunAsync<ReviewResult>(
                agents.FinalManager, review, "Manager", "manager-final", cycle);
        }

        throw new InvalidOperationException("Превышен лимит циклов workflow.");
    }

    private async Task<ManagerDecision> DecideAsync(object input, int cycle) =>
        await RunAsync<ManagerDecision>(
            agents.PolicyManager, input, "Manager", "manager-decision", cycle);

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
            $"Typed gate corrected invalid Manager route {decision.NextAgent}");
        return decision with
        {
            NextAgent = expected,
            Reason = $"Typed gate: {decision.NextAgent} не соответствует состоянию Developer; выбран {expected}."
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
            $"Typed gate corrected invalid Tester route {decision.NextAgent}");
        return decision with
        {
            NextAgent = expected,
            Reason = $"Typed gate: TestReport does not allow route {decision.NextAgent}; selected {expected}."
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
            $"Typed gate corrected invalid Reviewer route {decision.NextAgent}");
        return decision with
        {
            NextAgent = expected,
            Reason = blocking
                ? $"Typed gate: Reviewer blocking findings require Developer; route {decision.NextAgent} rejected."
                : $"Typed gate: Reviewer approved; Manager accepts route {decision.NextAgent}."
        };
    }

    private async Task<ImplementationResult> EnsureValidImplementationAsync(
        ArchitectureDecision architecture,
        ImplementationResult implementation,
        int cycle)
    {
        if (IsValidImplementation(implementation))
        {
            return implementation;
        }

        EnsureCycleAvailable(cycle, "Developer не применил patch и не вернул доказательства результата");
        TraceTransition("Developer", "Developer", cycle,
            "Governance вернул Developer за фактическими workspace changes");
        var corrected = await RunAsync<ImplementationResult>(
            agents.Developer,
            new ImplementationCorrectionRequest(
                architecture,
                implementation,
                "Нужны реальные вызовы ListWorkspaceFiles/ReadWorkspaceFile/ApplyWorkspacePatch и RunDotnetCheck; ChangedFiles должны подтверждаться diff.",
                cycle),
            "Developer", "implementation-correction", cycle);
        ValidateImplementation(corrected);
        return corrected;
    }

    private async Task<ArchitectureDecision> EnsureValidArchitectureAsync(
        ArchitectureDecision architecture,
        int cycle)
    {
        if (architecture.RequirementsAccepted && architecture.ChangedRequirements.Count > 0)
        {
            // The Architect cannot approve its own requirement changes. Discard the
            // unapproved list and keep the accepted decision; a real requirement
            // change still requires an explicit Manager approval and therefore has
            // to be represented by RequirementsAccepted=false.
            TraceTransition("Architect", "Manager", cycle,
                "Governance discarded unapproved ChangedRequirements");
            architecture = architecture with { ChangedRequirements = Array.Empty<string>() };
        }

        if (IsValidArchitecture(architecture))
        {
            return architecture;
        }

        EnsureCycleAvailable(cycle, "Architect вернул решение с изменёнными требованиями без approval");
        TraceTransition("Architect", "Architect", cycle,
            "Governance отклонил ArchitectureDecision с изменёнными требованиями");
        var corrected = await RunAsync<ArchitectureDecision>(
            agents.Architect,
            new ArchitectureCorrectionRequest(
                architecture,
                "RequirementsAccepted должен быть true, а ChangedRequirements должен быть пустым; не добавляй версии или новые требования.",
                cycle),
            "Architect", "architecture-correction", cycle);
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
            agent, input, metadata, agentName, step, iteration);

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
                "Developer изменил требования или архитектуру без ArchitectureQuestion.");
        }

        if (!implementation.NeedsClarification &&
            (!implementation.Implemented || implementation.ChangedFiles is null || implementation.ChangedFiles.Count == 0 ||
             string.IsNullOrWhiteSpace(implementation.WorkspaceRevision) ||
             string.IsNullOrWhiteSpace(implementation.DiffHash) ||
             implementation.ToolCalls is null || implementation.ToolCalls.Count == 0))
        {
            throw new InvalidOperationException(
                "Developer заявил, что уточнение не нужно, но не предоставил полный набор доказательств workspace: ChangedFiles, WorkspaceRevision, DiffHash и ToolCalls.");
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
                "Architect изменил или не принял требования без отдельного Manager approval.");
        }
    }

    private static bool IsValidArchitecture(ArchitectureDecision architecture) =>
        architecture.RequirementsAccepted && architecture.ChangedRequirements.Count == 0;
}

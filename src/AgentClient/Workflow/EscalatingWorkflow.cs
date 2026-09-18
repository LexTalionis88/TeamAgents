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
        ValidateArchitecture(architecture);

        for (var cycle = 1; cycle <= maxCycles + 1; cycle++)
        {
            var implementation = await RunAsync<ImplementationResult>(
                agents.Developer, architecture, "Developer", "implementation", cycle);
            ValidateImplementation(implementation);

            var developerDecision = await DecideAsync(
                new DeveloperEscalationInput(architecture, implementation, cycle), cycle);
            if (developerDecision.NextAgent.Equals("Architect", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCycleAvailable(cycle, "Developer запросил уточнение архитектуры");
                question = implementation.ArchitectureQuestion
                    ?? throw new InvalidOperationException(
                        "Developer выбрал эскалацию, но не вернул ArchitectureQuestion.");
                TraceTransition("Developer", "Architect", cycle, developerDecision.Reason);
                architecture = await RunAsync<ArchitectureDecision>(
                    agents.Architect, question, "Architect", "architecture", cycle + 1);
                ValidateArchitecture(architecture);
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
            var tests = await RunAsync<TestReport>(
                agents.Tester, implementation, "Tester", "testing", cycle);
            var testDecision = await DecideAsync(
                new TestEscalationInput(implementation, tests, cycle), cycle);

            if (testDecision.NextAgent.Equals("Developer", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCycleAvailable(cycle, "Tester вернул findings для повторной реализации");
                TraceTransition("Tester", "Developer", cycle, testDecision.Reason);
                continue;
            }

            EnsureRoute(testDecision, "Security", "Tester");
            var security = await RunAsync<SecurityReview>(
                agents.SecurityReviewer, tests, "Security", "security", cycle);
            var securityDecision = await DecideAsync(
                new SecurityEscalationInput(tests, security, cycle), cycle);

            if (securityDecision.NextAgent.Equals("Developer", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCycleAvailable(cycle, "Security вернул findings для повторной реализации");
                TraceTransition("Security", "Developer", cycle, securityDecision.Reason);
                continue;
            }

            EnsureRoute(securityDecision, "Reviewer", "Security");
            var review = await RunAsync<ReviewResult>(
                agents.Reviewer, security, "Reviewer", "review", cycle);
            return await RunAsync<ReviewResult>(
                agents.FinalManager, review, "Manager", "manager-final", cycle);
        }

        throw new InvalidOperationException("Превышен лимит циклов workflow.");
    }

    private async Task<ManagerDecision> DecideAsync(object input, int cycle) =>
        await RunAsync<ManagerDecision>(
            agents.PolicyManager, input, "Manager", "manager-decision", cycle);

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
        if (cycle > maxCycles)
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
    }

    private static void ValidateArchitecture(ArchitectureDecision architecture)
    {
        if (!architecture.RequirementsAccepted || architecture.ChangedRequirements.Count > 0)
        {
            throw new InvalidOperationException(
                "Architect изменил или не принял требования без отдельного Manager approval.");
        }
    }
}

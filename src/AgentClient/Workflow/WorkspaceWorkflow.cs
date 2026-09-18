using AgentClient.Agents;
using AgentClient.Infrastructure.Observability;
using Microsoft.Agents.AI.Workflows;

namespace AgentClient.Workflow;

public static class WorkspaceWorkflow
{
    public static Microsoft.Agents.AI.Workflows.Workflow Create(
        AgentSet agents,
        WorkflowRunMetadata metadata,
        System.Diagnostics.ActivitySource activitySource)
    {
        var manager = ((Func<ArchitectureQuestion, ValueTask<ArchitectureQuestion>>)(question =>
            TypedAgentRunner.RunAsync<ArchitectureQuestion>(
                agents.Manager, question, metadata, "Manager", "manager-intake", 1)))
            .BindAsExecutor("Manager-intake");
        var architect = ((Func<ArchitectureQuestion, ValueTask<ArchitectureDecision>>)(question =>
            TypedAgentRunner.RunAsync<ArchitectureDecision>(
                agents.Architect, question, metadata, "Architect", "architecture", 1)))
            .BindAsExecutor("Architect");
        var developer = ((Func<ArchitectureDecision, ValueTask<ImplementationResult>>)(decision =>
            TypedAgentRunner.RunAsync<ImplementationResult>(
                agents.Developer, decision, metadata, "Developer", "implementation", 1)))
            .BindAsExecutor("Developer");
        var tester = ((Func<ImplementationResult, ValueTask<TestReport>>)(result =>
            TypedAgentRunner.RunAsync<TestReport>(
                agents.Tester, result, metadata, "Tester", "testing", 1)))
            .BindAsExecutor("Tester");
        var security = ((Func<TestReport, ValueTask<SecurityReview>>)(report =>
            TypedAgentRunner.RunAsync<SecurityReview>(
                agents.SecurityReviewer, report, metadata, "Security", "security", 1)))
            .BindAsExecutor("Security");
        var reviewer = ((Func<SecurityReview, ValueTask<ReviewResult>>)(result =>
            TypedAgentRunner.RunAsync<ReviewResult>(
                agents.Reviewer, result, metadata, "Reviewer", "review", 1)))
            .BindAsExecutor("Reviewer");
        var finalManager = ((Func<ReviewResult, ValueTask<ReviewResult>>)(result =>
            TypedAgentRunner.RunAsync<ReviewResult>(
                agents.FinalManager, result, metadata, "Manager", "manager-final", 1)))
            .BindAsExecutor("Manager-final");

        return new WorkflowBuilder(manager)
            .AddEdge<ArchitectureQuestion>(source: manager, target: architect, label: "Manager -> Architect")
            .AddEdge<ArchitectureDecision>(source: architect, target: developer, label: "Architect -> Developer")
            .AddEdge<ImplementationResult>(source: developer, target: tester, label: "Developer -> Tester")
            .AddEdge<TestReport>(source: tester, target: security, label: "Tester -> Security")
            .AddEdge<SecurityReview>(source: security, target: reviewer, label: "Security -> Reviewer")
            .AddEdge<ReviewResult>(source: reviewer, target: finalManager, label: "Reviewer -> Manager")
            .WithOutputFrom(finalManager)
            .WithOpenTelemetry(options => options.EnableSensitiveData = true, activitySource)
            .Build();
    }
}

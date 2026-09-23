namespace AgentClient.Workflow;

/// <summary>
/// Общий контракт запуска оркестрации.
/// </summary>
internal interface IWorkflowRunner
{
    Task<ReviewResult> RunAsync(ArchitectureQuestion initialQuestion);
}

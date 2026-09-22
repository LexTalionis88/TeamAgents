using System.Diagnostics;

namespace AgentClient.Infrastructure.Observability;

internal sealed record WorkflowRunMetadata(
    string TaskId,
    string CorrelationId,
    string Feature)
{
    /// <summary>
    /// Создаёт запуск с уникальными идентификаторами задачи и корреляции.
    /// </summary>
    /// <param name="feature">Имя функционального сценария workflow.</param>
    public static WorkflowRunMetadata Create(string feature) =>
        new(
            TaskId: Guid.NewGuid().ToString("N"),
            CorrelationId: Guid.NewGuid().ToString("N"),
            Feature: feature);

    /// <summary>
    /// Накладывает метаданные роли и шага на activity.
    /// </summary>
    /// <param name="activity">Activity, которую нужно обогатить.</param>
    /// <param name="agentName">Имя роли агента.</param>
    /// <param name="step">Имя workflow-шага.</param>
    /// <param name="iteration">Номер итерации.</param>
    public void Apply(Activity? activity, string agentName, string step, int iteration)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("task.id", TaskId);
        activity.SetTag("correlation.id", CorrelationId);
        activity.SetTag("feature", Feature);
        activity.SetTag("agent.name", agentName);
        activity.SetTag("step", step);
        activity.SetTag("iteration", iteration);
    }
}

internal static class WorkflowRunContext
{
    private static readonly AsyncLocal<WorkflowRunMetadata?> Metadata = new();
    private static readonly AsyncLocal<WorkflowStep?> Step = new();

    /// <summary>
    /// Возвращает метаданные текущего запуска в асинхронном контексте.
    /// </summary>
    public static WorkflowRunMetadata? CurrentMetadata => Metadata.Value;

    /// <summary>
    /// Устанавливает метаданные запуска до завершения возвращённого scope.
    /// </summary>
    /// <param name="metadata">Метаданные текущего запуска.</param>
    public static IDisposable Begin(WorkflowRunMetadata metadata)
    {
        var previous = Metadata.Value;
        Metadata.Value = metadata;
        return new RestoreScope(() => Metadata.Value = previous);
    }

    /// <summary>
    /// Устанавливает метаданные конкретного шага и восстанавливает прежний контекст после завершения.
    /// </summary>
    /// <param name="metadata">Метаданные запуска.</param>
    /// <param name="agentName">Имя роли агента.</param>
    /// <param name="step">Имя workflow-шага.</param>
    /// <param name="iteration">Номер итерации.</param>
    public static IDisposable BeginStep(
        WorkflowRunMetadata metadata,
        string agentName,
        string step,
        int iteration)
    {
        var previousMetadata = Metadata.Value;
        var previousStep = Step.Value;
        var currentStep = new WorkflowStep(metadata, agentName, step, iteration);

        Metadata.Value = metadata;
        Step.Value = currentStep;
        currentStep.Apply(Activity.Current);

        return new RestoreScope(() =>
        {
            Metadata.Value = previousMetadata;
            Step.Value = previousStep;
        });
    }

    /// <summary>
    /// Накладывает текущий контекст workflow на activity.
    /// </summary>
    /// <param name="activity">Activity, которую нужно обогатить.</param>
    public static void Apply(Activity activity)
    {
        var metadata = Metadata.Value;
        var step = Step.Value;
        if (metadata is not null && step is not null)
        {
            step.Apply(activity);
        }
        else if (metadata is not null)
        {
            metadata.Apply(activity, "workflow", "workflow", 0);
        }
    }

    private sealed record WorkflowStep(
        WorkflowRunMetadata Metadata,
        string AgentName,
        string Step,
        int Iteration)
    {
        /// <summary>
        /// Накладывает данные шага на activity.
        /// </summary>
        /// <param name="activity">Activity, которую нужно обогатить.</param>
        public void Apply(Activity? activity) =>
            Metadata.Apply(activity, AgentName, Step, Iteration);
    }

    private sealed class RestoreScope(Action restore) : IDisposable
    {
        /// <summary>
        /// Восстанавливает контекст, существовавший до открытия scope.
        /// </summary>
        public void Dispose() => restore();
    }
}

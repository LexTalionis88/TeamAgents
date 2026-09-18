using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AgentClient.Infrastructure.Observability;

public sealed class TelemetryScope : IDisposable
{
    public const string SourceName = "Workspace.AgentWorkflow";

    private readonly TracerProvider _provider;
    private readonly ActivityListener _listener;

    private TelemetryScope(TracerProvider provider, ActivityListener listener)
    {
        _provider = provider;
        _listener = listener;
        WorkflowActivitySource = new ActivitySource(SourceName);
    }

    public ActivitySource WorkflowActivitySource { get; }

    public static TelemetryScope Create()
    {
        var provider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("workspace-agent-client", serviceVersion: "1.0.0"))
            .AddSource(SourceName)
            .AddSource("Microsoft.Agents.AI")
            .AddSource("Microsoft.Agents.AI.Workflows")
            .AddConsoleExporter()
            .Build();

        var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == SourceName ||
                source.Name == "Microsoft.Agents.AI" ||
                source.Name == "Microsoft.Agents.AI.Workflows",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = WorkflowRunContext.Apply,
        };
        ActivitySource.AddActivityListener(listener);

        return new TelemetryScope(provider, listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        WorkflowActivitySource.Dispose();
        _provider.Dispose();
    }

    public WorkflowRunScope StartRun(WorkflowRunMetadata metadata)
    {
        var context = WorkflowRunContext.Begin(metadata);
        var activity = WorkflowActivitySource.StartActivity("workspace.task", ActivityKind.Internal);
        WorkflowRunContext.Apply(activity ?? new Activity("workspace.task").Start());
        return new WorkflowRunScope(context, activity);
    }

    public sealed class WorkflowRunScope(IDisposable context, Activity? activity) : IDisposable
    {
        public void Dispose()
        {
            activity?.Stop();
            context.Dispose();
        }
    }
}

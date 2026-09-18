using System.Text.Json;
using AgentClient.Infrastructure.Observability;
using Microsoft.Agents.AI;

namespace AgentClient.Workflow;

public static class TypedAgentRunner
{
    public static async ValueTask<T> RunAsync<T>(
        AIAgent agent,
        object input,
        WorkflowRunMetadata metadata,
        string agentName,
        string step,
        int iteration)
    {
        using var context = WorkflowRunContext.BeginStep(metadata, agentName, step, iteration);
        var json = JsonSerializer.Serialize(input, JsonSerializerOptions.Web);
        var response = await agent.RunAsync<T>(
            $"Входной typed-контракт в JSON:\n{json}\n\nВерни заполненный результат своего контракта.",
            serializerOptions: JsonSerializerOptions.Web);

        return response.Result;
    }
}

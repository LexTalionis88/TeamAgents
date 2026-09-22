using System.Diagnostics;
using AgentClient.Infrastructure.Observability;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace AgentClient.Infrastructure.Mcp;

internal sealed class WorkspaceMcpInvoker(
    IReadOnlyList<AITool> tools,
    ActivitySource activitySource,
    WorkflowRunMetadata metadata)
{
    /// <summary>
    /// Вызывает MCP-инструмент и создаёт span с его результатом.
    /// </summary>
    /// <param name="toolName">Имя MCP-инструмента.</param>
    /// <param name="arguments">Аргументы инструмента.</param>
    /// <param name="agentName">Имя роли, вызвавшей инструмент.</param>
    /// <param name="step">Имя workflow-шага.</param>
    /// <param name="iteration">Номер итерации.</param>
    /// <param name="cancellationToken">Токен отмены вызова.</param>
    public async Task<string> InvokeAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        string agentName,
        string step,
        int iteration,
        CancellationToken cancellationToken = default)
    {
        var tool = tools.FirstOrDefault(candidate =>
            candidate.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
        using var activity = activitySource.StartActivity($"execute_tool {toolName}");
        metadata.Apply(activity, agentName, step, iteration);
        activity?.SetTag("gen_ai.operation.name", "execute_tool");
        activity?.SetTag("gen_ai.tool.type", "function");
        activity?.SetTag("gen_ai.tool.name", toolName);

        string text;
        if (tool is McpClientTool mcpTool)
        {
            var result = await mcpTool.CallAsync(
                arguments.ToDictionary(
                    pair => pair.Key,
                    pair => (object?)pair.Value),
                cancellationToken: cancellationToken);
            text = string.Join(
                Environment.NewLine,
                result.Content.Select(content => content.ToString()));
            if (result.IsError is true)
            {
                text = $"MCP_ERROR\n{text}";
            }
        }
        else if (tool is AIFunction function)
        {
            var result = await function.InvokeAsync(
                new AIFunctionArguments(arguments.ToDictionary()),
                cancellationToken);
            text = result?.ToString() ?? string.Empty;
        }
        else
        {
            var runtimeType = tool?.GetType().AssemblyQualifiedName ?? tool?.GetType().FullName ?? "<missing>";
            throw new InvalidOperationException(
                $"MCP tool '{toolName}' is not invokable. Runtime type: {runtimeType}");
        }

        activity?.SetTag("gen_ai.tool.call.result", text);
        return text;
    }

    /// <summary>
    /// Применяет unified diff через MCP.
    /// </summary>
    /// <param name="patch">Patch для изменения workspace.</param>
    /// <param name="agentName">Имя роли, вызвавшей инструмент.</param>
    /// <param name="step">Имя workflow-шага.</param>
    /// <param name="iteration">Номер итерации.</param>
    /// <param name="cancellationToken">Токен отмены вызова.</param>
    public Task<string> ApplyPatchAsync(
        string patch,
        string agentName,
        string step,
        int iteration,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            "apply_workspace_patch",
            new Dictionary<string, object?> { ["patch"] = patch },
            agentName,
            step,
            iteration,
            cancellationToken);

    /// <summary>
    /// Полностью заменяет файл через MCP.
    /// </summary>
    /// <param name="relativePath">Относительный путь файла.</param>
    /// <param name="content">Новое содержимое файла.</param>
    /// <param name="agentName">Имя роли, вызвавшей инструмент.</param>
    /// <param name="step">Имя workflow-шага.</param>
    /// <param name="iteration">Номер итерации.</param>
    /// <param name="cancellationToken">Токен отмены вызова.</param>
    public Task<string> ReplaceFileAsync(
        string relativePath,
        string content,
        string agentName,
        string step,
        int iteration,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            "replace_workspace_file",
            new Dictionary<string, object?>
            {
                ["relativePath"] = relativePath,
                ["content"] = content,
            },
            agentName,
            step,
            iteration,
            cancellationToken);

    /// <summary>
    /// Получает машиночитаемое evidence текущего workspace через MCP.
    /// </summary>
    /// <param name="agentName">Имя роли, вызвавшей инструмент.</param>
    /// <param name="step">Имя workflow-шага.</param>
    /// <param name="iteration">Номер итерации.</param>
    /// <param name="cancellationToken">Токен отмены вызова.</param>
    public Task<string> GetEvidenceAsync(
        string agentName,
        string step,
        int iteration,
        CancellationToken cancellationToken = default) =>
        InvokeAsync("get_workspace_evidence", new Dictionary<string, object?>(), agentName, step, iteration, cancellationToken);
}

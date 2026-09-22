using System.Diagnostics;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using AgentClient;
using AgentClient.Infrastructure.Observability;
using Microsoft.Agents.AI;

namespace AgentClient.Workflow;

internal static class TypedAgentRunner
{
    private static readonly ActivitySource RequestActivitySource = new("Workspace.AgentWorkflow");

    private static readonly JsonSerializerOptions PromptJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async ValueTask<string> RunActionAsync(
        AIAgent agent,
        object input,
        WorkflowRunMetadata metadata,
        string agentName,
        string step,
        int iteration,
        string instruction,
        int timeoutSeconds = 90)
    {
        using var context = WorkflowRunContext.BeginStep(metadata, agentName, step, iteration);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var json = JsonSerializer.Serialize(input, PromptJsonOptions);
        using var activity = RequestActivitySource.StartActivity("agent.request", ActivityKind.Internal);
        var stopwatch = Stopwatch.StartNew();
        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("step", step);
        activity?.SetTag("iteration", iteration);
        activity?.SetTag("agent.request.kind", "action");
        activity?.SetTag("agent.request.input_chars", json.Length);
        activity?.SetTag("agent.request.timeout_seconds", timeoutSeconds);
        if (agentName.Equals("Developer", StringComparison.OrdinalIgnoreCase) &&
            step.EndsWith("-implement", StringComparison.OrdinalIgnoreCase))
        {
            instruction = "Верни строго JSON по ImplementationPatch с полями Patch и Summary. " +
                "Источник задачи — Requirements.question во входном контракте; не подменяй его инфраструктурной задачей. " +
                "Не изменяй MCP Server, AgentClient, workflow или typed contracts, если это не требуется напрямую исходной задачей. " +
                "Patch должен быть обычным git unified diff, принимаемым git apply: используй diff --git, " +
                "пути ---/+++, а каждую добавленную строку начинай с +. Не используй вызовы инструментов, " +
                "*** Begin Patch, Markdown fences, многоточия, псевдокод или текст вне JSON. Новые комментарии " +
                "и документация должны быть на русском. Сохрани " +
                "переданные требования и технологии. Реализуй запрошенный объём и верни один полный " +
                "применимый patch либо несколько полных patch, если это необходимо.";
        }

        try
        {
            var response = await agent.RunAsync(
            $"{instruction}\nВходной контракт:\n{json}",
            cancellationToken: timeout.Token);

            var content = response.ToString();
            stopwatch.Stop();
            activity?.SetTag("agent.response.output_chars", content.Length);
            activity?.SetTag("agent.request.elapsed_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("agent.request.succeeded", true);
            return content;
        }
        catch (Exception exception)
        {
            MarkFailure(activity, exception, stopwatch);
            throw;
        }
    }

    public static async ValueTask<T> RunAsync<T>(
        AIAgent agent,
        object input,
        WorkflowRunMetadata metadata,
        string agentName,
        string step,
        int iteration,
        bool requiresLocalTypedJson,
        int timeoutSeconds = 180)
    {
        using var context = WorkflowRunContext.BeginStep(metadata, agentName, step, iteration);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var json = JsonSerializer.Serialize(input, PromptJsonOptions);
        using var activity = RequestActivitySource.StartActivity("agent.request", ActivityKind.Internal);
        var stopwatch = Stopwatch.StartNew();
        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("step", step);
        activity?.SetTag("iteration", iteration);
        activity?.SetTag("agent.request.kind", "typed");
        activity?.SetTag("agent.request.input_chars", json.Length);
        activity?.SetTag("agent.request.timeout_seconds", timeoutSeconds);
        var propertyNames = string.Join(
            ", ",
            typeof(T).GetProperties()
                .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name)));
        var propertyShapes = string.Join(
            ", ",
            typeof(T).GetProperties()
                .Select(property =>
                    $"{JsonNamingPolicy.CamelCase.ConvertName(property.Name)}: {DescribeType(property.PropertyType)}"));
        var prompt = $"Верни ровно один JSON-объект для typed-контракта {typeof(T).Name}. " +
            $"Используй только эти поля в camelCase: {propertyNames}. " +
            "Не добавляй неизвестные поля, оболочки, псевдонимы, Markdown или текст вне JSON. " +
            "Заполни все поля контракта; для пустых списков используй [], для пустых строк — осмысленное значение. " +
            "Верни JSON в одну строку, правильно закрывай каждый массив и объект, а переносы строк внутри строк экранируй как \\n. " +
            (typeof(T) == typeof(TaskPlan)
                ? "Для TaskPlan поле unresolvedRequirements является только JSON-массивом строк; items является JSON-массивом WorkItem, а у каждого WorkItem поле dependencies является JSON-массивом строк. Не помещай объект requirements в unresolvedRequirements. "
                : string.Empty) +
            $"Входной контракт:\n{json}";
        prompt += $" Типы полей typed-контракта: {propertyShapes}. Соблюдай эти типы: массивы остаются JSON-массивами, строки — JSON-строками, логические значения — true или false.";
        if (typeof(T) == typeof(TaskPlan))
        {
            prompt += " Для TaskPlan каждый элемент items обязан содержать id, title, objective, acceptanceCriteria и dependencies; acceptanceCriteria и dependencies — JSON-массивы строк, даже если в массиве один элемент или он пуст. Соблюдай PlanLimits из входного контракта и не возвращай больше MaximumWorkItems элементов.";
        }

        if (!requiresLocalTypedJson)
        {
            try
            {
                var response = await agent.RunAsync<T>(
                    prompt,
                    serializerOptions: JsonSerializerOptions.Web,
                    cancellationToken: timeout.Token);
                stopwatch.Stop();
                activity?.SetTag("agent.response.output_chars", response.Result?.ToString()?.Length ?? 0);
                activity?.SetTag("agent.request.elapsed_ms", stopwatch.ElapsedMilliseconds);
                activity?.SetTag("agent.request.succeeded", true);
                return response.Result;
            }
            catch (Exception exception)
            {
                MarkFailure(activity, exception, stopwatch);
                throw;
            }
        }

        // Для некоторых режимов провайдера конечная точка вызова инструментов
        // может отклонять JSON Schema/response_format, который создаёт generic
        // RunAsync<T> Agent Framework. Сохраняем typed-контракт и governance-
        // проверки, запрашиваем JSON в prompt и выполняем локальную десериализацию.
        string content;
        try
        {
            var untypedResponse = await agent.RunAsync(prompt, cancellationToken: timeout.Token);
            content = untypedResponse.ToString().Trim();
        }
        catch (Exception exception)
        {
            MarkFailure(activity, exception, stopwatch);
            throw;
        }
        activity?.SetTag("agent.response.output_chars", content.Length);
        if (content.StartsWith("```", StringComparison.Ordinal))
        {
            content = RemoveMarkdownFence(content);
        }

        try
        {
            var result = Deserialize<T>(content);
            stopwatch.Stop();
            activity?.SetTag("agent.request.elapsed_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("agent.request.succeeded", true);
            return result;
        }
        catch (JsonException exception)
        {
            activity?.SetTag("agent.response.repair_attempted", true);
            activity?.SetTag("agent.response.initial_parse_error", true);
            var repairPrompt = prompt +
                $"\nПредыдущий ответ не является валидным JSON: {exception.Message}. " +
                "Исправь только формат и верни заново полный JSON-объект со всеми полями, без сокращений и пояснений." +
                $"\nПредыдущий ответ:\n{content}";
            try
            {
                var repairedResponse = await agent.RunAsync(repairPrompt, cancellationToken: timeout.Token);
                var repairedContent = RemoveMarkdownFence(repairedResponse.ToString().Trim());
                var result = Deserialize<T>(repairedContent);
                stopwatch.Stop();
                activity?.SetTag("agent.response.repair_attempted", true);
                activity?.SetTag("agent.response.output_chars", repairedContent.Length);
                activity?.SetTag("agent.request.elapsed_ms", stopwatch.ElapsedMilliseconds);
                activity?.SetTag("agent.request.succeeded", true);
                return result;
            }
            catch (Exception repairException)
            {
                MarkFailure(activity, repairException, stopwatch);
                throw;
            }
        }
    }

    private static void MarkFailure(Activity? activity, Exception exception, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        activity?.SetTag("agent.request.succeeded", false);
        activity?.SetTag("agent.request.elapsed_ms", stopwatch.ElapsedMilliseconds);
        activity?.SetTag("error.type", exception.GetType().FullName);
        activity?.SetTag("error.message", exception.Message);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    private static T Deserialize<T>(string content)
    {
        var node = JsonNode.Parse(content) ?? throw new JsonException("Провайдер вернул пустой JSON.");
        if (node is JsonObject jsonObject)
        {
            NormalizeCollectionValues(jsonObject, typeof(T));
        }

        return JsonSerializer.Deserialize<T>(node.ToJsonString(), JsonSerializerOptions.Web)
            ?? throw new JsonException($"Провайдер вернул пустой typed-контракт для {typeof(T).Name}.");
    }

    private static void NormalizeCollectionValues(JsonObject jsonObject, Type contractType)
    {
        foreach (var property in contractType.GetProperties())
        {
            var jsonName = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            if (!jsonObject.TryGetPropertyValue(jsonName, out var node) || node is null)
            {
                continue;
            }

            var elementType = GetCollectionElementType(property.PropertyType);
            if (elementType == typeof(string) && node is JsonValue stringValue)
            {
                try
                {
                    jsonObject[jsonName] = new JsonArray(stringValue.GetValue<string>());
                }
                catch (InvalidOperationException)
                {
                    // Неподходящее значение остаётся исходным и будет отклонено типизированной десериализацией.
                }

                continue;
            }

            if (elementType is not null && elementType != typeof(string) && node is JsonArray array)
            {
                foreach (var item in array.OfType<JsonObject>())
                {
                    NormalizeCollectionValues(item, elementType);
                }
            }
        }
    }

    private static Type? GetCollectionElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        return type.IsGenericType &&
               typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
            ? type.GetGenericArguments().LastOrDefault()
            : null;
    }

    private static string DescribeType(Type type)
    {
        var nullableType = Nullable.GetUnderlyingType(type) ?? type;
        if (nullableType == typeof(string))
        {
            return "строка";
        }

        if (nullableType == typeof(bool))
        {
            return "логическое значение";
        }

        if (nullableType.IsArray ||
            (nullableType.IsGenericType &&
             typeof(System.Collections.IEnumerable).IsAssignableFrom(nullableType)))
        {
            var elementType = nullableType.IsArray
                ? nullableType.GetElementType()
                : nullableType.GetGenericArguments().LastOrDefault();
            return $"JSON-массив {DescribeType(elementType ?? typeof(object))}";
        }

        if (nullableType.IsPrimitive || nullableType.IsEnum || nullableType == typeof(decimal))
        {
            return nullableType.Name;
        }

        return "JSON-объект";
    }

    private static string RemoveMarkdownFence(string content)
    {
        var firstNewLine = content.IndexOf('\n');
        var closingFence = content.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewLine >= 0 && closingFence > firstNewLine
            ? content[(firstNewLine + 1)..closingFence].Trim()
            : content;
    }
}

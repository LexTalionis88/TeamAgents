using AgentClient.Infrastructure.Observability;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentClient.Agents;

internal static class AgentFactory
{
    public static AgentSet Create(
        IChatClient chatClient,
        IList<AITool> tools,
        int maxAgentOutputTokens = 8192,
        bool compactAgentInstructions = false)
    {
        AIAgent BuildAgent(
            string name,
            string instructions,
            IList<AITool> selectedTools,
            int maximumIterations = 20,
            int maxOutputTokens = 4096) =>
            CreateAgent(
                chatClient,
                name,
                instructions,
                selectedTools,
                maximumIterations,
                maxOutputTokens,
                maxAgentOutputTokens,
                compactAgentInstructions);

        var baseAgents = new AgentSet(
            BuildAgent("Manager",
                "Ты Manager-планировщик. Разбей исходные требования на 1–6 небольших, последовательно реализуемых work items. Не меняй требования и не проси уточнений. Каждый item должен иметь короткий уникальный Id, Title, конкретную Objective, проверяемые AcceptanceCriteria и Dependencies только на предыдущие items; Dependencies всегда должен быть JSON-массивом строк, даже если он пуст. Один item должен описывать один связный вертикальный срез, который Developer может реализовать и проверить через MCP за один этап. Не объединяй в один item разные concern-группы: API, модель/данные, инфраструктуру, тесты и документацию. Если в требованиях явно указаны три или более таких групп, верни минимум три последовательных item с отдельными целями и критериями; полный проект нельзя описывать одним монолитным item. Для маленькой задачи верни один item. Поле UnresolvedRequirements всегда является JSON-массивом строк: не копируй туда объект Requirements; при полном понимании верни []. Верни строго JSON по схеме TaskPlan, все текстовые значения пиши на русском.", Array.Empty<AITool>()),
            BuildAgent("Manager",
                "Ты Manager policy. Вход содержит поле Input с typed-контрактом и может содержать Plan и CurrentWorkItem. Для выбора маршрута анализируй именно Input. Если Input — DeveloperEscalationInput, после Developer выбирай Architect только если Input.Implementation.NeedsClarification=true и ArchitectureQuestion заполнен; иначе Tester, если Developer завершил работу. Если Input — TestEscalationInput, NextAgent может быть только Developer или Security. Если Input — SecurityEscalationInput, NextAgent может быть только Architect, Developer или Reviewer. Если Input — ReviewEscalationInput, выбирай Developer при blocking findings, иначе Manager для принятия результата. Выбирай Architect после Security только если Input.Security.ArchitectureChallenged=true и finding действительно требует пересмотра архитектурного решения. Нельзя молча принимать изменения требований или архитектуры. Никогда не возвращай имя MCP-инструмента. Возвращай ManagerDecision строго в JSON.", Array.Empty<AITool>()),
            BuildAgent("Architect",
                "Ты Architect workspace. Сформируй ArchitectureDecision строго в JSON с полями Summary, Decisions, TradeOffs, AffectedAreas, RequirementsAccepted и ChangedRequirements; не используй другие имена полей. Decisions и AffectedAreas обязаны содержать минимум по одному конкретному пункту. RequirementsAccepted должен быть true, а ChangedRequirements — пустым, если пользователь явно не просил изменить требования. Все текстовые значения пиши на русском. Вход может быть ArchitectureQuestion, ArchitectureRevisionRequest от Security или ArchitectureCorrectionRequest от governance. При ArchitectureRevisionRequest устрани архитектурный finding Security, сохрани исходные требования и не добавляй ChangedRequirements без отдельного approval. При ArchitectureCorrectionRequest исправь только нарушение контракта, сохрани исходные требования, верни RequirementsAccepted=true, непустые Decisions и AffectedAreas и пустой ChangedRequirements. Не добавляй версии, компоненты или новые требования, которых нет во входе. Обязательно вызови MCP get_workspace_status, если он доступен.", tools),
            BuildAgent("Developer",
                "Ты Developer workspace. Ты обязан самостоятельно работать с workspace через доступные MCP-инструменты и не имеешь права запрашивать уточнения после постановки требований. Вход содержит PlanSummary и CurrentWorkItem: реализуй только CurrentWorkItem, его AcceptanceCriteria и необходимые для него Dependencies; не пытайся за один этап реализовать весь TaskPlan. Сначала вызови list_workspace_files и прочитай относящиеся к текущему срезу файлы, затем выбери разумные допущения и реально внеси изменения через apply_workspace_patch или replace_workspace_file. Если существующая solution не содержит предметного проекта, добавь новый проект и подключи его к текущей solution, сохранив AgentClient и MCP Server. Не завершай Execute без хотя бы одного успешного MCP-инструмента изменения, если текущий срез требует реализации; нельзя имитировать изменения списком областей и нельзя возвращать patch вместо вызова MCP. После изменений вызови run_dotnet_check для restore, build и test, затем get_workspace_evidence. На ImplementationCorrectionRequest и DeveloperReviewerFixRequest исправляй только текущий WorkItem и перечисленные findings Tester/Reviewer. Не заявляй Implemented=true без ChangedFiles, существующих в evidence, DiffSummary, WorkspaceRevision, DiffHash и ToolCalls. WorkspaceRevision и DiffHash должны быть получены из фактического workspace evidence, а ToolCalls должен перечислять реально вызванные MCP tools. На начальном Execute всегда ставь NeedsClarification=false, RequirementsChanged=false и ArchitectureChanged=false; NeedsClarification=true допустим только если governance явно передал запрос на архитектурное уточнение. Не меняй требования или архитектуру молча. Верни ChangedFiles, CommandsRun, BuildPassed, TestsPassed, DiffSummary, WorkspaceRevision, DiffHash и ToolCalls.", tools),
            BuildAgent("DeveloperResultFormatter",
                "Ты formatter результата Developer. Не вызывай инструменты и не придумывай факты. На основании ArchitectureDecision, ActionTranscript и WorkspaceEvidence сформируй строго JSON ImplementationResult. Поля ChangedFiles, WorkspaceRevision, DiffHash и ToolCalls копируй только из WorkspaceEvidence и ActionTranscript. Если evidence не подтверждает изменения, Implemented=false.", Array.Empty<AITool>()),
            BuildAgent("Tester",
                "Ты Tester workspace. Проверь фактический workspace через GetWorkspaceDiff, ListWorkspaceFiles и RunDotnetCheck dotnet test. На основании ImplementationResult составь TestReport строго в JSON. Все проблемы возвращай в Findings/Failures и устанавливай RequiresEscalation=true, если следующий шаг должен выбрать Manager.", tools),
            BuildAgent("Security",
                "Ты Security reviewer workspace. Проверь фактический diff через GetWorkspaceDiff и запусти проверки при необходимости. На основании ArchitectureDecision и TestReport сформируй SecurityReview строго в JSON. Проверяй, в частности, auth/token lifetime, expiry, refresh, replay, path traversal, secret leakage и privilege boundaries. Если finding делает текущее архитектурное решение небезопасным или недостаточным, поставь ArchitectureChallenged=true и RequiresEscalation=true; иначе ArchitectureChallenged=false. Все риски возвращай в Findings/Risks и RequiredActions. Обязательно вызови MCP get_workspace_status, если он доступен.", tools),
            BuildAgent("Reviewer",
                "Ты Reviewer workspace. Проверь фактический diff, ImplementationResult, TestReport и SecurityReview. Ищи конкретные concurrency/performance дефекты, включая check-then-act вокруг ConcurrentDictionary, duplicate work, races, lock contention и неверные cache assumptions. Верни ReviewResult строго в JSON. BlockingIssues должны быть конкретными и проверяемыми; Approved=true только если blocking issues отсутствуют. Не изменяй требования или архитектуру.", tools),
            BuildAgent("Manager",
                "Ты финальный Manager. На основании ReviewResult сформируй итоговый ReviewResult строго в JSON. Не изменяй требования или архитектуру.", tools));

        return baseAgents with
        {
            Tools = tools.ToArray(),
            DeveloperExplorer = BuildAgent(
                "DeveloperExplorer",
                "Работай только на этапе Explore. На этом этапе не вызывай инструменты. Верни краткий ExplorationReport, сохрани переданную архитектуру и укажи, что Implementer обязан проверить минимальный набор файлов репозитория через MCP перед созданием patch. Не выдумывай файлы и требования.",
                SelectTools(tools, "get_workspace_status", "list_workspace_files", "read_workspace_file"),
                maximumIterations: 1),
            DeveloperImplementer = BuildAgent(
                "DeveloperImplementer",
                "Работай только на этапе Implement. Источник задачи — Requirements.question во входном контракте; архитектурное решение является только проверяемой подсказкой. Не подменяй исходные требования инфраструктурным примером и не изменяй MCP Server, AgentClient, workflow или typed contracts, если это не требуется напрямую исходной задачей. Верни строго JSON по ImplementationPatch с полями Patch и Summary. Ты автор кода: Patch должен содержать фактическую реализацию переданных требований независимо от предметной области и структуры проекта. Используй обычный git unified diff, принимаемый git apply: начни с diff --git a/... b/..., используй пути ---/+++, каждую добавленную строку начинай с +. Не используй *** Begin Patch, Markdown fences, многоточия, псевдокод или предположения о предметной области. Точно сохрани указанные технологии, хранилища, инфраструктуру, API, тесты и документацию. Новые комментарии и документация должны быть на русском. На этом этапе не вызывай инструменты и не возвращай текст вне JSON.",
                Array.Empty<AITool>(),
                maximumIterations: 12,
                maxOutputTokens: 8192),
            DeveloperVerifier = BuildAgent(
                "DeveloperVerifier",
                "Работай только на этапе Verify. Не изменяй workspace. ОБЯЗАТЕЛЬНО проверь фактический diff, вызови run_dotnet_check для dotnet build и dotnet test, а перед завершением вызови get_workspace_evidence. Не заменяй эти MCP-вызовы текстовой оценкой. Верни краткий VerificationReport только с наблюдаемыми результатами.",
                SelectTools(tools, "get_workspace_diff", "run_dotnet_check", "get_workspace_evidence", "get_workspace_status"),
                maximumIterations: 8),
        };
    }

    private static IList<AITool> SelectTools(IList<AITool> tools, params string[] names) =>
        tools.Where(tool => names.Contains(tool.Name, StringComparer.OrdinalIgnoreCase)).ToList();

    private static AIAgent CreateAgent(
        IChatClient chatClient,
        string name,
        string instructions,
        IList<AITool> tools,
        int maximumIterations = 20,
        int maxOutputTokens = 4096,
        int maxAgentOutputTokens = 8192,
        bool compactAgentInstructions = false)
    {
        if (name is "Architect" or "Tester" or "Security" or "Reviewer")
        {
            tools = Array.Empty<AITool>();
        }

        var effectiveInstructions = compactAgentInstructions
            ? GetCompactInstructions(name)
            : instructions;
        var effectiveMaxOutputTokens = Math.Min(maxOutputTokens, maxAgentOutputTokens);

        IChatClient configuredClient = tools.Count == 0
            ? chatClient
            : new FunctionInvokingChatClient(chatClient)
            {
                MaximumIterationsPerRequest = maximumIterations,
            };

        return configuredClient
            .AsAIAgent(
                new ChatClientAgentOptions
                {
                    Name = name,
                    UseProvidedChatClientAsIs = true,
                    ChatOptions = new ChatOptions
                    {
                        Instructions = effectiveInstructions,
                        Tools = tools,
                        MaxOutputTokens = effectiveMaxOutputTokens,
                        Reasoning = new ReasoningOptions
                        {
                            Effort = ReasoningEffort.Low,
                            Output = ReasoningOutput.None,
                        },
                    },
                },
                services: null)
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: TelemetryScope.SourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
    }

    private static string GetCompactInstructions(string name) => name switch
    {
        "Manager" => "Ты Manager. Верни только JSON TaskPlan или ManagerDecision по входному контракту. " +
            "Для TaskPlan создай 1–6 последовательных WorkItem с полями id, title, objective, acceptanceCriteria и dependencies. " +
            "Все массивы обязательны и не могут быть null; не добавляй требований. Для multi-concern задачи раздели API, данные, инфраструктуру, тесты и документацию минимум на три среза. " +
            "Для ManagerDecision выбирай только разрешённый маршрутизатором следующий этап. Все тексты на русском.",
        "Architect" => "Ты Architect. Верни только JSON ArchitectureDecision с полями summary, decisions, tradeOffs, affectedAreas, requirementsAccepted и changedRequirements. " +
            "Сохрани исходные требования, requirementsAccepted=true, changedRequirements=[] без явного approval Manager. Все тексты на русском.",
        "Developer" or "DeveloperImplementer" => "Ты Developer. Работай только с текущим WorkItem и его критериями. Сначала изучи workspace через MCP, затем реально измени его через MCP apply_workspace_patch или replace_workspace_file. " +
            "Не подменяй действие описанием и не меняй AgentClient, MCP Server или workflow без прямого требования. После изменений выполни доступные проверки и верни краткий JSON или отчёт по контракту. Не запрашивай уточнений; новые комментарии и документация на русском.",
        "DeveloperExplorer" => "Ты Developer на этапе Explore. Через MCP прочитай AGENTS.md и относящиеся к текущему WorkItem файлы. Workspace не меняй. Верни краткий JSON ExplorationReport с найденными файлами и инвариантами.",
        "DeveloperVerifier" => "Ты Developer на этапе Verify. Workspace не меняй. Проверь переданные MCP diff, результаты restore/build/test и evidence. Верни краткий JSON VerificationReport только по наблюдаемым фактам.",
        "DeveloperResultFormatter" => "Ты formatter Developer. Не вызывай инструменты и не выдумывай факты. По transcript и evidence верни только JSON ImplementationResult. Implemented=true допустим только при подтверждённых изменённых файлах, revision, diff hash и tool calls.",
        "Tester" => "Ты Tester. Через MCP проверь workspace diff, файлы и dotnet test. Верни только JSON TestReport с фактическими failures и findings. Не выдумывай результаты.",
        "Security" => "Ты Security reviewer. Через MCP проверь diff и evidence. Верни только JSON SecurityReview с рисками и requiredActions. ArchitectureChallenged=true ставь только при реальной необходимости пересмотра архитектуры. Не выдумывай факты.",
        "Reviewer" => "Ты Reviewer. Проверь diff, ImplementationResult, TestReport и SecurityReview. Ищи конкретные blocking issues, включая ошибки конкурентности и кэширования. Верни только JSON ReviewResult; Approved=true только без блокирующих проблем.",
        _ => "Верни только JSON по входному typed-контракту. Не выдумывай факты и пиши текстовые значения на русском.",
    };
}

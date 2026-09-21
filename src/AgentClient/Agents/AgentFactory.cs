using AgentClient.Infrastructure.Observability;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentClient.Agents;

public static class AgentFactory
{
    public static AgentSet Create(IChatClient chatClient, IList<AITool> tools)
    {
        var baseAgents = new AgentSet(
            CreateAgent(chatClient, "Manager",
                "Ты Manager intake. Нормализуй ArchitectureQuestion без изменения требований. Верни строго JSON по схеме ArchitectureQuestion.", tools),
            CreateAgent(chatClient, "Manager",
                "Ты Manager policy. Поле Stage определяет gate. После Stage=Developer выбирай Architect только если Implementation.NeedsClarification=true и Implementation.ArchitectureQuestion заполнен; иначе выбирай Tester, если Developer завершил работу. После Stage=Tester NextAgent может быть только Developer или Security. После Stage=Security NextAgent может быть только Architect, Developer или Reviewer. После Stage=Reviewer выбирай Developer при blocking findings, иначе Manager для принятия результата. Выбирай Architect после Security только если Security.ArchitectureChallenged=true и finding действительно требует пересмотра архитектурного решения. Нельзя молча принимать изменения требований или архитектуры. Никогда не возвращай имя MCP-инструмента. Возвращай ManagerDecision строго в JSON.", Array.Empty<AITool>()),
            CreateAgent(chatClient, "Architect",
                "Ты Architect workspace. Сформируй ArchitectureDecision строго в JSON. Вход может быть ArchitectureQuestion, ArchitectureRevisionRequest от Security или ArchitectureCorrectionRequest от governance. При ArchitectureRevisionRequest устрани архитектурный finding Security, сохрани исходные требования и не добавляй ChangedRequirements без отдельного approval. При ArchitectureCorrectionRequest исправь только нарушение контракта, сохрани исходные требования, верни RequirementsAccepted=true и пустой ChangedRequirements. Не добавляй версии, компоненты или новые требования, которых нет во входе. Обязательно вызови MCP get_workspace_status, если он доступен.", tools),
            CreateAgent(chatClient, "Developer",
                "Ты Developer workspace. Ты обязан самостоятельно работать с workspace. Сначала вызови ListWorkspaceFiles и ReadWorkspaceFile, затем реально реализуй ArchitectureDecision через ApplyWorkspacePatch; нельзя имитировать изменения списком областей. После patch вызови RunDotnetCheck для build и dotnet test. На ImplementationCorrectionRequest и DeveloperReviewerFixRequest немедленно исправь перечисленные findings Tester/Reviewer и вызови tools. Не заявляй Implemented=true без ChangedFiles, существующих в diff, DiffSummary, WorkspaceRevision, DiffHash и ToolCalls. WorkspaceRevision и DiffHash должны быть получены из фактического workspace evidence, а ToolCalls должен перечислять реально вызванные MCP tools. Если архитектура неоднозначна, поставь NeedsClarification=true и заполни ArchitectureQuestion. Если уточнение не нужно, поставь NeedsClarification=false, RequirementsChanged=false и ArchitectureChanged=false. Верни ChangedFiles, CommandsRun, BuildPassed, TestsPassed, DiffSummary, WorkspaceRevision, DiffHash и ToolCalls. Не меняй требования или архитектуру молча.", tools),
            CreateAgent(chatClient, "DeveloperResultFormatter",
                "Ты formatter результата Developer. Не вызывай инструменты и не придумывай факты. На основании ArchitectureDecision, ActionTranscript и WorkspaceEvidence сформируй строго JSON ImplementationResult. Поля ChangedFiles, WorkspaceRevision, DiffHash и ToolCalls копируй только из WorkspaceEvidence и ActionTranscript. Если evidence не подтверждает изменения, Implemented=false.", Array.Empty<AITool>()),
            CreateAgent(chatClient, "Tester",
                "Ты Tester workspace. Проверь фактический workspace через GetWorkspaceDiff, ListWorkspaceFiles и RunDotnetCheck dotnet test. На основании ImplementationResult составь TestReport строго в JSON. Все проблемы возвращай в Findings/Failures и устанавливай RequiresEscalation=true, если следующий шаг должен выбрать Manager.", tools),
            CreateAgent(chatClient, "Security",
                "Ты Security reviewer workspace. Проверь фактический diff через GetWorkspaceDiff и запусти проверки при необходимости. На основании ArchitectureDecision и TestReport сформируй SecurityReview строго в JSON. Проверяй, в частности, auth/token lifetime, expiry, refresh, replay, path traversal, secret leakage и privilege boundaries. Если finding делает текущее архитектурное решение небезопасным или недостаточным, поставь ArchitectureChallenged=true и RequiresEscalation=true; иначе ArchitectureChallenged=false. Все риски возвращай в Findings/Risks и RequiredActions. Обязательно вызови MCP get_workspace_status, если он доступен.", tools),
            CreateAgent(chatClient, "Reviewer",
                "Ты Reviewer workspace. Проверь фактический diff, ImplementationResult, TestReport и SecurityReview. Ищи конкретные concurrency/performance дефекты, включая check-then-act вокруг ConcurrentDictionary, duplicate work, races, lock contention и неверные cache assumptions. Верни ReviewResult строго в JSON. BlockingIssues должны быть конкретными и проверяемыми; Approved=true только если blocking issues отсутствуют. Не изменяй требования или архитектуру.", tools),
            CreateAgent(chatClient, "Manager",
                "Ты финальный Manager. На основании ReviewResult сформируй итоговый ReviewResult строго в JSON. Не изменяй требования или архитектуру.", tools));

        return baseAgents with
        {
            DeveloperExplorer = CreateAgent(
                chatClient,
                "DeveloperExplorer",
                "Only perform Explore. Use read-only MCP tools to inspect AGENTS.md, the needed memory bank documents, and the minimum source files. Do not change files, call patch, or run dotnet. Return a concise ExplorationReport with components, invariants, files, and plan.",
                SelectTools(tools, "get_workspace_status", "list_workspace_files", "read_workspace_file"),
                maximumIterations: 6),
            DeveloperImplementer = CreateAgent(
                chatClient,
                "DeveloperImplementer",
                "Only perform Implement. Read the ExplorationReport and necessary files, then make real changes through ApplyWorkspacePatch. Do not pretend changes and do not return typed ImplementationResult. Return a concise ImplementationActionReport.",
                SelectTools(tools, "get_workspace_status", "list_workspace_files", "read_workspace_file", "apply_workspace_patch"),
                maximumIterations: 12),
            DeveloperVerifier = CreateAgent(
                chatClient,
                "DeveloperVerifier",
                "Only perform Verify. Do not change workspace. Inspect the diff, run allowed dotnet build/test checks, and obtain machine-readable workspace evidence. Return a concise VerificationReport.",
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
        int maximumIterations = 20)
    {
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
                        Instructions = instructions,
                        Tools = tools,
                    },
                },
                services: null)
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: TelemetryScope.SourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
    }
}

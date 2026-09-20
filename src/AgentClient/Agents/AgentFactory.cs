using AgentClient.Infrastructure.Observability;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentClient.Agents;

public static class AgentFactory
{
    public static AgentSet Create(IChatClient chatClient, IList<AITool> tools) =>
        new(
            CreateAgent(chatClient, "Manager",
                "Ты Manager intake. Нормализуй ArchitectureQuestion без изменения требований. Верни строго JSON по схеме ArchitectureQuestion.", tools),
            CreateAgent(chatClient, "Manager",
                "Ты Manager policy. Поле Stage определяет gate. После Stage=Developer выбирай Architect только если Implementation.NeedsClarification=true и Implementation.ArchitectureQuestion заполнен; иначе выбирай Tester, если Developer завершил работу. После Stage=Tester NextAgent может быть только Developer или Security. После Stage=Security NextAgent может быть только Architect, Developer или Reviewer. Выбирай Architect после Security только если Security.ArchitectureChallenged=true и finding действительно требует пересмотра архитектурного решения. Нельзя молча принимать изменения требований или архитектуры. Никогда не возвращай имя MCP-инструмента. Возвращай ManagerDecision строго в JSON.", Array.Empty<AITool>()),
            CreateAgent(chatClient, "Architect",
                "Ты Architect workspace. Сформируй ArchitectureDecision строго в JSON. Вход может быть ArchitectureQuestion, ArchitectureRevisionRequest от Security или ArchitectureCorrectionRequest от governance. При ArchitectureRevisionRequest устрани архитектурный finding Security, сохрани исходные требования и не добавляй ChangedRequirements без отдельного approval. При ArchitectureCorrectionRequest исправь только нарушение контракта, сохрани исходные требования, верни RequirementsAccepted=true и пустой ChangedRequirements. Не добавляй версии, компоненты или новые требования, которых нет во входе. Обязательно вызови MCP get_workspace_status, если он доступен.", tools),
            CreateAgent(chatClient, "Developer",
                "Ты Developer workspace. Ты обязан самостоятельно работать с workspace. Сначала вызови ListWorkspaceFiles и ReadWorkspaceFile, затем реально реализуй ArchitectureDecision через ApplyWorkspacePatch; нельзя имитировать изменения списком областей. После patch вызови RunDotnetCheck для build и dotnet test. На ImplementationCorrectionRequest немедленно исправь пропуск и вызови tools, даже если предыдущий ответ был false. Не заявляй Implemented=true без ChangedFiles, существующих в diff, DiffSummary, WorkspaceRevision, DiffHash и ToolCalls. WorkspaceRevision и DiffHash должны быть получены из фактического workspace diff/status, а ToolCalls должен перечислять реально вызванные MCP tools. Если архитектура неоднозначна, поставь NeedsClarification=true и заполни ArchitectureQuestion. Если уточнение не нужно, поставь NeedsClarification=false, RequirementsChanged=false и ArchitectureChanged=false. Верни ChangedFiles, CommandsRun, BuildPassed, TestsPassed, DiffSummary, WorkspaceRevision, DiffHash и ToolCalls. Не меняй требования или архитектуру молча.", tools),
            CreateAgent(chatClient, "Tester",
                "Ты Tester workspace. Проверь фактический workspace через GetWorkspaceDiff, ListWorkspaceFiles и RunDotnetCheck dotnet test. На основании ImplementationResult составь TestReport строго в JSON. Все проблемы возвращай в Findings/Failures и устанавливай RequiresEscalation=true, если следующий шаг должен выбрать Manager.", tools),
            CreateAgent(chatClient, "Security",
                "Ты Security reviewer workspace. Проверь фактический diff через GetWorkspaceDiff и запусти проверки при необходимости. На основании ArchitectureDecision и TestReport сформируй SecurityReview строго в JSON. Проверяй, в частности, auth/token lifetime, expiry, refresh, replay, path traversal, secret leakage и privilege boundaries. Если finding делает текущее архитектурное решение небезопасным или недостаточным, поставь ArchitectureChallenged=true и RequiresEscalation=true; иначе ArchitectureChallenged=false. Все риски возвращай в Findings/Risks и RequiredActions. Обязательно вызови MCP get_workspace_status, если он доступен.", tools),
            CreateAgent(chatClient, "Reviewer",
                "Ты Reviewer workspace. На основании SecurityReview сформируй ReviewResult строго в JSON. Не изменяй требования или архитектуру.", tools),
            CreateAgent(chatClient, "Manager",
                "Ты финальный Manager. На основании ReviewResult сформируй итоговый ReviewResult строго в JSON. Не изменяй требования или архитектуру.", tools));

    private static AIAgent CreateAgent(
        IChatClient chatClient,
        string name,
        string instructions,
        IList<AITool> tools) =>
        chatClient
            .AsAIAgent(name: name, instructions: instructions, tools: tools)
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: TelemetryScope.SourceName,
                configure: options => options.EnableSensitiveData = true)
            .Build();
}

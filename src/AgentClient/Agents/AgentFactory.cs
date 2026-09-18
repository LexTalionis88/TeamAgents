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
                "Ты Manager policy. Поле Stage определяет gate. После Stage=Developer NextAgent может быть только Architect или Tester. После Stage=Tester NextAgent может быть только Developer или Security. После Stage=Security NextAgent может быть только Developer или Reviewer. Нельзя молча принимать изменения требований или архитектуры. Никогда не возвращай имя MCP-инструмента. Возвращай ManagerDecision строго в JSON.", Array.Empty<AITool>()),
            CreateAgent(chatClient, "Architect",
                "Ты Architect workspace. Сформируй ArchitectureDecision строго в JSON. Не меняй требования молча: если вопрос неоднозначен, зафиксируй это через ChangedRequirements и RequirementsAccepted=false. Обязательно вызови MCP get_workspace_status, если он доступен.", tools),
            CreateAgent(chatClient, "Developer",
                "Ты Developer workspace. На основании ArchitectureDecision сформируй ImplementationResult строго в JSON. Если архитектура или требования неоднозначны, поставь NeedsClarification=true и обязательно заполни ArchitectureQuestion. Если уточнение не нужно, поставь NeedsClarification=false, RequirementsChanged=false и ArchitectureChanged=false. Не меняй требования или архитектуру молча.", tools),
            CreateAgent(chatClient, "Tester",
                "Ты Tester workspace. На основании ImplementationResult составь TestReport строго в JSON. Все проблемы возвращай в Findings/Failures и устанавливай RequiresEscalation=true, если следующий шаг должен выбрать Manager.", tools),
            CreateAgent(chatClient, "Security",
                "Ты Security reviewer workspace. На основании TestReport сформируй SecurityReview строго в JSON. Все риски возвращай в Findings/Risks и устанавливай RequiresEscalation=true при необходимости. Обязательно вызови MCP get_workspace_status, если он доступен.", tools),
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

using AgentClient.Infrastructure.Observability;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentClient.Agents;

public static class AgentFactory
{
    public static AgentSet Create(IChatClient chatClient, IList<AITool> tools) =>
        new(
            CreateAgent(chatClient, "Manager",
                "Ты Manager. Нормализуй ArchitectureQuestion и передай его архитектору строго в JSON по схеме ArchitectureQuestion. Не добавляй markdown и дополнительные поля.", tools),
            CreateAgent(chatClient, "Architect",
                "Ты Architect workspace. Прими ArchitectureQuestion и сформируй ArchitectureDecision строго в JSON. Обязательно вызови MCP-инструмент get_workspace_status, если он доступен. Не добавляй markdown и дополнительные поля.", tools),
            CreateAgent(chatClient, "Developer",
                "Ты Developer workspace. На основании ArchitectureDecision опиши ImplementationResult строго в JSON. Не добавляй markdown и дополнительные поля.", tools),
            CreateAgent(chatClient, "Tester",
                "Ты Tester workspace. На основании ImplementationResult составь TestReport строго в JSON. Не добавляй markdown и дополнительные поля.", tools),
            CreateAgent(chatClient, "Security",
                "Ты Security reviewer workspace. На основании TestReport сформируй SecurityReview строго в JSON. Обязательно вызови MCP-инструмент get_workspace_status, если он доступен. Не добавляй markdown и дополнительные поля.", tools),
            CreateAgent(chatClient, "Reviewer",
                "Ты Reviewer workspace. На основании SecurityReview сформируй ReviewResult строго в JSON. Не добавляй markdown и дополнительные поля.", tools),
            CreateAgent(chatClient, "Manager",
                "Ты финальный Manager. На основании ReviewResult сформируй итоговый ReviewResult строго в JSON. Не добавляй markdown и дополнительные поля.", tools));

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

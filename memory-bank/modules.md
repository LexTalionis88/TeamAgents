# Модули и функциональность

Текущие технические модули:

- `src/McpServer/Program.cs` — отдельный MCP host со stdio transport и
  `WorkspaceTools`. Публичные tools сейчас: `get_workspace_status` и `echo`.
  Сервер не знает об Agent Framework и не должен зависеть от `AgentClient`.
- `src/AgentClient/Application/AgentClientApplication.cs` — composition root:
  читает конфигурацию, подключает MCP, Ollama, telemetry и запускает workflow.
- `src/AgentClient/Infrastructure/Mcp/McpServerConnection.cs` — запускает
  `McpServer` как дочерний `dotnet`-процесс, делает discovery tools и передаёт
  их агентам.
- `src/AgentClient/Agents/` — фабрика и набор агентов: Manager intake,
  PolicyManager, Architect, Developer, Tester, Security и Reviewer.
- `src/AgentClient/Workflow/` — typed execution и orchestration с эскалациями;
  `EscalatingWorkflow.cs` задаёт допустимые переходы и лимит циклов.
- `src/AgentClient/Contracts/WorkflowContracts.cs` — typed records между
  агентами. Это внутренний workflow-контракт, но его поля проверяются runtime и
  должны изменяться совместимо с producer/consumer.
- `src/AgentClient/Infrastructure/Observability/` — correlation metadata,
  workflow spans и console exporter.

Предметная область пока не определена; система является техническим примером
интеграции MCP, Agent Framework и локального Ollama.

Для изменения workflow сначала исследовать `AgentClientApplication.cs`,
`AgentFactory.cs`, `WorkflowContracts.cs` и `EscalatingWorkflow.cs`. Для нового
MCP tool — `src/McpServer/Program.cs`, затем `McpServerConnection.cs` и `api.md`.

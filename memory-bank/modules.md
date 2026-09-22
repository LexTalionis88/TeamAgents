# Модули и функциональность

## Граница автономной реализации

`EscalatingWorkflow` не зависит от предметной области. Он отвечает только за
типизированную маршрутизацию, ограниченные review/fix-циклы, MCP-доказательства
и независимую от модели валидацию. Требования пользователя являются входом
задачи; детали реализации, пути файлов и предметный repair-код создаются
Developer через MCP.

Создание клиентов моделей изолировано в `src/AgentClient/Infrastructure/Ai`.
Реализации `IChatClientProvider` владеют деталями endpoint, модели и учётных
данных; `AgentClientApplication` зависит только от `ChatClientProviderFactory`.

Текущие технические модули:

- `src/McpServer/Program.cs` — только точка композиции отдельного MCP host со
  stdio transport. Сервер не знает об Agent Framework и не должен зависеть от
  `AgentClient`.
- `src/McpServer/Abstractions/` — интерфейсы сервисов путей, процессов, Git и
  workspace snapshot. Это внутренние границы MCP-сборки, а не публичный API.
- `src/McpServer/Services/` — реализации инфраструктурных сервисов MCP:
  разрешение путей, запуск процессов, применение Git patch и сбор workspace
  snapshot.
- `src/McpServer/Tools/` — отдельные классы MCP-инструментов статуса, файлов,
  patch, .NET-проверок и workspace evidence. Инструмент patch поддерживает Git
  unified diff и формат `*** Begin Patch` для добавления новых файлов.
- `src/AgentClient/Application/AgentClientApplication.cs` — корень композиции:
  читает конфигурацию, подключает MCP и выбранный адаптер модельного провайдера,
  настраивает telemetry и запускает workflow.
- `src/AgentClient/Infrastructure/Mcp/McpServerConnection.cs` — запускает
  `McpServer` как дочерний `dotnet`-процесс, делает discovery tools и передаёт
  их агентам; класс является внутренней деталью AgentClient.
- `src/AgentClient/Agents/` — фабрика и набор агентов: Manager-планировщик,
  PolicyManager, Architect, Developer, Tester, Security и Reviewer.
- `src/AgentClient/Workflow/` — типизированное выполнение и оркестрация с эскалациями;
  `EscalatingWorkflow.cs` задаёт декомпозицию на WorkItem, допустимые переходы,
  последовательную передачу срезов и лимит review/fix-циклов.
- `src/AgentClient/Contracts/WorkflowContracts.cs` — typed records между
  агентами, включая `TaskPlan` и `WorkItem`. Это внутренний workflow-контракт,
  но его поля проверяются runtime и должны изменяться совместимо с
  producer/consumer.
- `src/AgentClient/Infrastructure/Observability/` — correlation metadata,
  spans workflow и console exporter.

Предметная область пока не определена; система является техническим примером
интеграции MCP, Agent Framework и локального Ollama.

Для изменения workflow сначала исследовать `AgentClientApplication.cs`,
`AgentFactory.cs`, `WorkflowContracts.cs` и `EscalatingWorkflow.cs`. Для нового
MCP-инструмента — соответствующий класс в `src/McpServer/Tools/`, его сервисы в
`src/McpServer/Services/`, затем `McpServerConnection.cs` и `api.md`.

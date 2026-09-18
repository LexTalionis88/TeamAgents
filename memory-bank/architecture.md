# Архитектура и навигация по репозиторию

## Текущее состояние

Workspace содержит [`workspace.slnx`](../workspace.slnx) с двумя консольными
проектами: `src/McpServer` и `src/AgentClient`.

## Проекты и слои

Текущая карта:

- **`src/McpServer`** — MCP host и инструменты workspace; не содержит Agent
  Framework и не зависит от клиента.
- **`src/AgentClient`** — host Microsoft Agent Framework: запускает MCP-сервер по
  stdio, обнаруживает MCP-инструменты и передаёт их модели через function calling.
- **`tests/`** — пока отсутствует; добавлять сюда тесты протокола и логики tools.

Предметные Domain/Application слои пока не выделены: текущий пример — технический
вертикальный срез интеграции.

## Разрешённые зависимости

Основное направление зависимостей:

```text
AgentClient -> MCP protocol -> McpServer
AgentClient -> Microsoft Agent Framework -> model provider
Tests -> проверяемый проект
```

McpServer не должен зависеть от AgentClient. MCP-сервер не пишет диагностические
сообщения в stdout, поскольку stdout используется транспортом MCP.

## Entry points

Текущий исполняемый маршрут workflow: Manager intake -> Architect -> Developer
-> Tester -> Security -> Reviewer -> Manager final. Manager policy принимает
решения об эскалации после Developer, Tester и Security. Реализация маршрута и
проверок находится в `src/AgentClient/Workflow/EscalatingWorkflow.cs`; не следует
выводить маршрут только из prompt-ов агентов.

Correlation и metadata запуска описаны в docs/ai/observability/correlation.md. Корневой workflow span и typed executor spans получают task.id, correlation.id, feature, agent.name, step и iteration.

AgentClient исполняет typed workflow: `ArchitectureQuestion -> ArchitectureDecision -> ImplementationResult -> TestReport -> SecurityReview -> ReviewResult`. Описание контрактов: [`docs/ai/observability/typed-contracts.md`](../docs/ai/observability/typed-contracts.md).

Multi-agent workflow и его observability описаны в
[`docs/ai/observability/agent-workflow.md`](../docs/ai/observability/agent-workflow.md).
Граф не строится в `Program.cs`: orchestration выполняется классом
`EscalatingWorkflow`, а `Program.cs` только вызывает application entry point.

Основные entry points — `src/McpServer/Program.cs` и
`src/AgentClient/Program.cs`. Сервер запускается командой `dotnet run` и общается
через stdio; клиент запускается командой `dotnet run --project src/AgentClient`.

## Внешние системы

Внешние системы: локальный Ollama через `OllamaSharp` и
MCP-протокол между двумя локальными процессами. Облачные AI-провайдеры не
используются. RabbitMQ пока не подключён; его topology и маршрут сообщений
зафиксированы в [`docs/ai/modules/rabbitmq.md`](../docs/ai/modules/rabbitmq.md)
как незаполненные до появления интеграции.

## Основные data/control flows

Основной поток:

```text
AgentClient -> запускает McpServer -> initialize/list tools -> Agent Framework
-> модель выбирает tool -> MCP tools/call по stdio -> McpServer -> результат модели
```

Ошибки запуска процесса и MCP-протокола возвращаются клиенту; ключи и endpoint
берутся только из переменных окружения.

## Где искать код для типичных изменений

| Тип изменения | Сначала исследовать |
|---|---|
| MCP-инструмент или серверный контракт | `src/McpServer/Program.cs` |
| Agent Framework, prompt или model provider | `src/AgentClient/Agents/AgentFactory.cs`, `src/AgentClient/Application/AgentClientApplication.cs` |
| MCP process transport | оба `Program.cs`, затем `README.md` |
| Сборка, запуск или конфигурация | `*.csproj`, `global.json`, `README.md` |
| Инвариант предметной области | `docs/ai/glossary-and-invariants.md` |

При каждом добавлении или перемещении проекта обновляй разделы «Проекты и слои»,
«Entry points», «Внешние системы» и эту таблицу в том же изменении.

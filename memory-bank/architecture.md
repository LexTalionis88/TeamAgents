# Архитектура и навигация по репозиторию

## Граница оркестрации без предметной области

`EscalatingWorkflow` является переиспользуемым оркестратором, а не реализацией
acceptance-примера. Он не должен распознавать задачу по имени, предписывать её
файлы, создавать каркас конкретного проекта или содержать предметный repair-код.
Такая задача, как Short URL или Like, передаётся только в содержимом
`ArchitectureQuestion` и реализуется агентами через общий MCP-путь patch/evidence.

Та же граница действует для создания клиентов моделей: `AgentClientApplication`
не знает OpenAI-совместимые endpoint, учётные данные провайдеров или типы Ollama.
Эти детали принадлежат реализациям `IChatClientProvider` в
`src/AgentClient/Infrastructure/Ai/Providers` и выбираются через
`ChatClientProviderFactory`.

Провайдеры также объявляют технические capability для агентских вызовов:
безопасный бюджет вывода и необходимость компактного профиля инструкций. Эти
ограничения остаются в адаптере конкретного провайдера; orchestration-код не
содержит ветвлений по Groq, Gemini или другому поставщику.

## Текущее состояние

Workspace содержит [`workspace.slnx`](../workspace.slnx) с двумя консольными
проектами: `src/McpServer` и `src/AgentClient`.

## Проекты и слои

## Целевой автономный сценарий

Система должна пройти полноценную production-like задачу без ручных подсказок
после initial requirements. Базовый acceptance scenario — Short URL или Like
service на ASP.NET Core с API, PostgreSQL, Redis, Docker и tests.

Пользователь общается только с Manager. Manager выбирает роли и gate-маршрут,
Developer выполняет реальные изменения через MCP, Tester/Security/Reviewer
проверяют результат, а PolicyManager разрешает только bounded review/fix cycles.
Финальный ответ обязан опираться на workspace evidence и trace, а не только на
текстовое утверждение модели.

Текущая карта:

- **`src/McpServer`** — MCP host и инструменты workspace; не содержит Agent
  Framework и не зависит от клиента. `Program.cs` только регистрирует DI,
  транспорт и классы tools; инфраструктурная логика находится в сервисах.
  `apply_workspace_patch` принимает обычный Git unified diff и совместимый
  формат `*** Begin Patch` для добавления новых файлов, а изменения существующих
  файлов выполняются через unified diff или `replace_workspace_file`.
  Реализации сервисов, модели инфраструктуры, DI-интерфейсы и классы tools не
  являются публичным API сборки и имеют уровень `internal`; наружу они доступны
  только через MCP protocol.
- **`src/AgentClient`** — host Microsoft Agent Framework: запускает MCP-сервер по
  stdio, обнаруживает MCP-инструменты и передаёт их модели через function calling.
  Реализации провайдеров, workflow, telemetry, MCP connection и configuration
  также имеют уровень `internal`; публичными остаются typed workflow-контракты.
- **`tests/Workspace.Tests`** — NUnit unit, MCP stdio integration и explicit
  Ollama E2E tests.

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

Текущий исполняемый маршрут workflow: Manager decomposition -> Architect -> Developer
-> Tester -> Security -> Reviewer -> следующий WorkItem или Manager final. Manager policy принимает
решения об эскалации после Developer, Tester и Security. Security может явно
оспорить архитектуру: тогда маршрут становится Security -> Manager policy ->
Architect -> Developer, а новый `ArchitectureDecision` заменяет прежний.
Реализация маршрута и проверок находится в
`src/AgentClient/Workflow/EscalatingWorkflow.cs`; не следует выводить маршрут
только из prompt-ов агентов.

Correlation и metadata запуска описаны в docs/ai/observability/correlation.md. Корневой workflow span и typed executor spans получают task.id, correlation.id, feature, agent.name, step и iteration.

AgentClient сначала получает от Manager `TaskPlan` с последовательными
`WorkItem`, затем исполняет для каждого среза typed workflow:
`ArchitectureQuestion -> ArchitectureDecision -> ImplementationResult -> TestReport -> SecurityReview -> ReviewResult`.
Только после одобрения текущего среза workflow передаёт Developer следующий
`WorkItem`; review/fix-циклы остаются bounded. Описание контрактов: [`docs/ai/observability/typed-contracts.md`](../docs/ai/observability/typed-contracts.md).

Multi-agent workflow и его observability описаны в
[`docs/ai/observability/agent-workflow.md`](../docs/ai/observability/agent-workflow.md).
Граф не строится в `Program.cs`: orchestration выполняется классом
`EscalatingWorkflow`, а `Program.cs` только вызывает application entry point.

Итоговое decision по agent team зафиксировано в
[`docs/ai/decisions/final-agent-team-architecture.md`](../docs/ai/decisions/final-agent-team-architecture.md).
Пользовательский вход принадлежит Manager; маршрутизация выполняется
детерминированным policy-слоем поверх Microsoft Agent Framework, а роли
Architect, Developer, Tester, Security и Reviewer работают через typed
contracts и bounded review/fix cycles.

Основные entry points — `src/McpServer/Program.cs` и
`src/AgentClient/Program.cs`. Сервер запускается командой `dotnet run` и общается
через stdio; клиент запускается командой `dotnet run --project src/AgentClient`.

## Внешние системы

Внешние системы: локальный Ollama через `OllamaSharp`, OpenRouter/Gemini/Groq через
OpenAI-compatible `IChatClient` и MCP-протокол между двумя локальными
процессами. Gemini и Groq используют локальную typed JSON-десериализацию в
`TypedAgentRunner`, чтобы схема ответа провайдера не конфликтовала с вызовом
функций; typed JSON валидируется локально.
RabbitMQ пока не подключён; его topology и маршрут сообщений
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
| MCP-инструмент или серверный контракт | `src/McpServer/Tools/`, `src/McpServer/Abstractions/`, `api.md` |
| Agent Framework, prompt или model provider | `src/AgentClient/Agents/AgentFactory.cs`, `src/AgentClient/Application/AgentClientApplication.cs` |
| MCP process transport | оба `Program.cs`, затем `README.md` |
| Сборка, запуск или конфигурация | `*.csproj`, `global.json`, `README.md` |
| Инвариант предметной области | `docs/ai/glossary-and-invariants.md` |

При каждом добавлении или перемещении проекта обновляй разделы «Проекты и слои»,
«Entry points», «Внешние системы» и эту таблицу в том же изменении.

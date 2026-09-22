# Итоговая архитектура .NET agent team

## Архитектурная граница acceptance-примера

Short URL и Like являются примерами для проверки команды. Они не встраиваются
в `EscalatingWorkflow` или prompt-ы агентов как шаблоны реализации. Workflow
применяет один общий протокол MCP patch, build/test и workspace evidence к любым
требованиям пользователя; конкретные файлы и реализацию выбирают Developer-агенты.

Провайдеры моделей также являются адаптерами, а не логикой application-слоя.
Groq, Gemini, OpenRouter и Ollama реализуют `IChatClientProvider` в каталоге
`Infrastructure/Ai/Providers`; приложение выбирает провайдер через
`ChatClientProviderFactory` и не зависит от SDK конкретного провайдера.

## Решение

### Целевой acceptance scenario

Архитектура предназначена для автономного выполнения полноценной .NET-задачи
после одной постановки требований: например, Short URL или Like service на
ASP.NET Core с API, PostgreSQL, Redis, Docker и тестами. Пользователь не даёт
агентам промежуточных указаний. Manager самостоятельно выбирает маршрут и роли,
Developer меняет workspace через MCP, Tester/Security/Reviewer выполняют
проверки и bounded review/fix cycles, а итог подтверждается workspace evidence,
diff и trace.

Пользователь взаимодействует только с логической ролью `Manager`. Manager
принимает задачу и запускает ограниченный typed workflow. Специализированные
агенты не являются пользовательским интерфейсом и не принимают внешние
решения напрямую.

Исполняемая команда использует Microsoft Agent Framework для `AIAgent`,
structured output, executor spans и OpenTelemetry. Детерминированная policy-
логика в `EscalatingWorkflow` отвечает за допустимые переходы, typed gates и
лимит итераций; LLM не может самостоятельно изменить граф маршрутизации.

## Роли

- `Manager` — intake, policy gates и финальная агрегация.
- `Architect` — ArchitectureDecision и пересмотр решения после security finding.
- `Developer` — изменение workspace через MCP tools.
- `Tester` — проверка diff и dotnet tests.
- `Security` — security review и архитектурное оспаривание.
- `Reviewer` — финальная проверка SecurityReview.

Минимальный обязательный набор для production-like изменения: Manager,
Developer, Tester и Security. Architect подключается при архитектурной
неопределённости или `ArchitectureChallenged=true`; Reviewer остаётся финальным
quality gate.

## Основной маршрут

```text
User
  -> Manager intake
  -> Architect
  -> Developer
  -> Tester
  -> Security
  -> Reviewer
  -> Manager final
```

Исправления Tester образуют bounded loop:

```text
Developer -> Tester
              | failed TestReport
              v
           Manager -> DeveloperTesterFixRequest -> Developer
                                                -> Tester
```

Security может запустить отдельный цикл:

```text
Security(ArchitectureChallenged=true)
  -> Manager policy
  -> Architect(ArchitectureRevisionRequest)
  -> Developer
```

Оба цикла ограничены `WORKFLOW_MAX_CYCLES`, по умолчанию двумя итерациями.
Каждый переход записывается как `workflow.transition` с `from.agent`,
`to.agent`, `iteration` и `manager.reason`.

## Typed contracts

Основные сообщения:

```text
ArchitectureQuestion
  -> ArchitectureDecision
  -> ImplementationResult
  -> TestReport
  -> SecurityReview
  -> ReviewResult
```

`DeveloperTesterFixRequest` передаёт Developer предыдущую реализацию и полный
`TestReport`. `ImplementationResult` обязан содержать доказательства результата:
`ChangedFiles`, `CommandsRun`, `BuildPassed`, `TestsPassed`, `DiffSummary`,
`WorkspaceRevision`, `DiffHash` и `ToolCalls`.

`ReviewerInput` объединяет реализацию, тестовый отчёт и security review.
Конкретный concurrency/performance finding передаётся через
`DeveloperReviewerFixRequest`; Manager принимает только ReviewerResult без
blocking issues.

MCP `get_workspace_evidence` возвращает machine-readable revision, SHA-256
текущего diff и список изменённых файлов. Поэтому текстовый ответ модели не
считается доказательством изменения workspace сам по себе.

## Observability

Каждый запуск имеет `task.id`, `correlation.id` и `feature`. В trace видны:

- `workflow.session` и `workflow_invoke`;
- `executor.process` для typed executor;
- `invoke_agent` и `chat` для LLM-вызовов;
- `workflow.transition` для эскалаций;
- `tool_call` и результат MCP tool, если модель действительно вызвала tool.

Коммуникация восстанавливается по correlation ID, `agent.name`, `step` и
`iteration`. Наличие tool definition без `tool_call` не считается выполнением
инструмента.

## Acceptance matrix

| Критерий | Реализация |
|---|---|
| Пользователь говорит только с Manager | `AgentClientApplication` создаёт Manager intake и Manager final |
| Минимум 4 специализированных агента | Manager, Architect, Developer, Tester, Security, Reviewer |
| Microsoft Agent Framework | `AIAgent`, `AIAgent.RunAsync<T>`, `Microsoft.Agents.AI.Workflows`, OpenTelemetry integration |
| Typed contracts | `WorkflowContracts.cs`, generic `RunAsync<T>` |
| Review/fix cycle | `DeveloperTesterFixRequest` и `ArchitectureRevisionRequest` |
| Восстановление workflow по trace | correlation metadata, executor spans и transition spans |

## Ограничения

Локальная модель может вернуть structured JSON без фактического function call.
Поэтому workflow обязан проверять workspace evidence и останавливать ложный
успех. Для простых CRUD-задач полный multi-agent маршрут избыточен; тогда
допустим один Developer с обязательным build/test gate.

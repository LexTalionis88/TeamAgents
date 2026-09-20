# Typed-контракты между агентами

## Цепочка

Текущий workflow в `src/AgentClient/Workflow/EscalatingWorkflow.cs` передаёт данные между агентами только через типизированные records:

```text
ArchitectureQuestion
  -> ArchitectureDecision
  -> ImplementationResult
  -> TestReport
  -> SecurityReview
  -> ReviewResult
```

Каждый переход является generic edge с типом результата предыдущего executor-а.
OpenTelemetry показывает типы в `executor.type` и `message.type`.

## Контракты

| Контракт | Роль |
|---|---|
| `ArchitectureQuestion` | исходный вопрос, контекст и ограничения |
| `ArchitectureDecision` | архитектурное решение и затронутые области |
| `ImplementationResult` | результат реализации, оставшаяся работа и доказательства workspace (`ChangedFiles`, `WorkspaceRevision`, `DiffHash`, `ToolCalls`) |
| `TestReport` | проверки, ошибки и рекомендации |
| `SecurityReview` | findings, риски и обязательные действия |
| `ReviewResult` | итоговое решение, блокирующие проблемы и следующие шаги |

Для security gate используются дополнительные контракты:

| Контракт | Роль |
|---|---|
| `SecurityReviewInput` | текущая архитектура и результаты тестирования для Security |
| `SecurityEscalationInput` | архитектура, тесты и SecurityReview для Manager policy |
| `ArchitectureRevisionRequest` | finding Security и текущая архитектура для Architect |

`SecurityReview.ArchitectureChallenged=true` — единственное typed-условие,
разрешающее переход Security -> Architect.

## Structured output

Каждый агент вызывается через `AIAgent.RunAsync<T>`. Framework запрашивает JSON-схему по generic-типу и десериализует результат в `AgentResponse<T>`. Свободный текст не используется как межагентный контракт.

Локальная модель Ollama должна поддерживать structured output. Если модель вернула JSON, который не соответствует схеме, workflow завершается ошибкой десериализации, а не передаёт неопределённый текст дальше.

В текущем демонстрационном режиме включён `EnableSensitiveData`, поэтому в
консольных spans видны входные и выходные JSON. Перед production-использованием
это следует отключить или настроить redaction.

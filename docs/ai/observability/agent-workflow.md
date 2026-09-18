# Наблюдаемость multi-agent workflow

## Назначение

Актуальная реализация workflow состоит из пяти последовательных агентов и typed-контрактов. Исторический пример маршрутизатора ниже сохранён для сравнения, а текущая схема описана в typed-contracts.md.

`src/AgentClient/Program.cs` содержит минимальный workflow из двух агентов:

```text
запрос -> маршрутизатор -> агент-аналитик
                    \-> агент-ревьюер
```

Маршрутизатор выбирает ветку детерминированно. Если запрос содержит `проверь` или `ревью`, запускается ревьюер; иначе запускается аналитик. Оба агента используют локальную модель Ollama и обнаруженные через MCP tools.

## Что наблюдать в консоли

Клиент одновременно выводит события workflow и экспортирует OpenTelemetry-трассы через `OpenTelemetry.Exporter.Console`.

- `workflow.build` — построенный граф и его определение.
- `workflow.session` и `workflow_invoke` — границы одного запуска workflow.
- `executor.process` — вход и результат конкретного executor-а.
- `edge_group.process` — переход по ребру; `delivered` показывает выбранную ветку, `dropped condition false` — отвергнутую условную ветку.
- `message.send` — передача сообщения между executor-ами.
- `invoke_agent` и `chat` — запуск агента и обращение к Ollama.
- В `chat` видны `tool_call` и ответ MCP tool; это позволяет установить, какой инструмент был вызван.

Помимо трасс, строки `[WORKFLOW]` показывают `WorkflowStartedEvent`, `ExecutorInvokedEvent`, `ExecutorCompletedEvent`, `WorkflowOutputEvent` и завершение super-step.

## Воспроизведение

Нужны локально запущенные Ollama и модель:

```text
ollama serve
ollama pull qwen3:1.7b
dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"
dotnet run --project src/AgentClient -- "Проанализируй текущий запрос"
```

Первый запрос должен выбрать `агент-ревьюер` и вызвать `get_workspace_status`. Второй должен выбрать `агент-аналитик`. Имя модели можно изменить через `OLLAMA_MODEL`, endpoint — через `OLLAMA_HOST`.

## Важные ограничения

В демонстрации включён `EnableSensitiveData`, поэтому в консоль попадают prompt, tool arguments и ответы модели. Это удобно для исследования порядка выполнения, но не должно включаться без оценки риска в production. Для безопасного режима отключите эту опцию в настройках workflow и агентов.

OpenTelemetry показывает причинно-следственную картину выполнения workflow. `dotnet-monitor` в Docker Compose решает другую задачу: сбор диагностических данных .NET-процесса через diagnostic socket. Он не заменяет workflow-трассировку.

Поддержка workflow и его spans реализована через `Microsoft.Agents.AI.Workflows` и `.WithOpenTelemetry(...)`; консольный вывод включён намеренно, чтобы пример работал без внешнего коллектора.

# Correlation и metadata workflow

## Модель идентификации

Каждый запуск AgentClient создаёт:

- task.id — идентификатор конкретной задачи;
- correlation.id — идентификатор, по которому восстанавливается вся трасса запуска;
- feature — функциональная область запуска, по умолчанию workspace-architecture-review. Значение можно изменить через WORKFLOW_FEATURE.

Эти значения записываются в корневой span workspace.task и повторяются на spans workflow и агентов.

## Metadata шага

Для каждого typed executor-а добавляются:

- agent.name — логическое имя агента;
- step — architecture, implementation, testing, security или review;
- iteration — номер итерации шага. В текущем линейном workflow это 1; при добавлении retry/циклов значение нужно увеличивать.

Контекст хранится в AsyncLocal, поэтому не передаётся вручную через каждый контракт и сохраняется в асинхронном вызове агента и Ollama.

## Как проверять

Запустите:

dotnet run --project src/AgentClient -- "Проверь correlation"

В консоли найдите одну строку запуска с task_id и correlation_id, затем убедитесь, что те же значения присутствуют в spans executor.process, invoke_agent и chat. Полный путь должен содержать шаги:

architecture -> implementation -> testing -> security -> review

Для поиска в OTLP backend используйте фильтр по correlation.id. Для текущего console exporter достаточно скопировать это значение из строки запуска.

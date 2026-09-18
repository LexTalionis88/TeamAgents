# Разбор trace запуска workflow

## Проверенный запуск

Команда:

dotnet run --project src/AgentClient --no-build -- "Проверь статус MCP и спроектируй безопасное изменение"

Идентификаторы запуска:

- task.id: 2a0e4bdf6e334109a8d69bdfc4d6f92d
- correlation.id: d6897ea459ae4a7dab8519f79009a408
- feature: workspace-architecture-review

## Восстановленный путь

По message.source_id, message.target_id, executor.process и invoke_agent восстановлена последовательность:

Manager-intake
  -> Architect
  -> Developer
  -> Tester
  -> Security
  -> Reviewer
  -> Manager-final

Manager-intake и Manager-final имеют разные executor IDs из-за разных типов входа, но оба имеют agent.name=Manager. Это одна логическая роль Manager в начале и конце workflow.

На каждом агентском шаге наблюдались:

- одинаковые task.id и correlation.id;
- feature=workspace-architecture-review;
- собственные agent.name и step;
- iteration=1.

## LLM-вызовы

Обнаружено семь invoke_agent и соответствующих chat spans:

1. Manager: manager-intake;
2. Architect: architecture;
3. Developer: implementation;
4. Tester: testing;
5. Security: security;
6. Reviewer: review;
7. Manager: manager-final.

Повторных вызовов и retry не обнаружено: каждый шаг имеет одну итерацию. Два вызова Manager ожидаемы для заявленного маршрута, но являются потенциально лишними, если начальный Manager будет заменён детерминированным приёмом задачи, а финальный — агрегацией без LLM.

## Tool calls

В этом конкретном trace MCP tools были доступны в gen_ai.tool.definitions, но tool_call не обнаружен. Ни Architect, ни Security не вызвали get_workspace_status. Причина — модель qwen3:1.7b в режиме RunAsync<T> выбрала structured JSON без function call; наличие tool definition не означает фактический вызов.

Это место коммуникации непрозрачно: по одному только итоговому ArchitectureDecision нельзя доказать, что MCP status был проверен. Для production-контракта проверку нужно делать детерминированным executor-ом или добавить отдельный audit result, который фиксирует факт вызова MCP.

## Вывод

Trace позволяет восстановить полный порядок участников и переходов по одному correlation.id. Он также показывает отсутствие retries и tool calls. Для анализа причин решения включён EnableSensitiveData, поэтому доступны prompt, structured JSON и ответы модели; в production эти данные нужно редаct-ить.

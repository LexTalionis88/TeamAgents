# Developer action loop

Цикл действий использует отдельные этапы с ограниченными списками MCP-разрешений:
`DeveloperExplorer` работает только на чтение, основной `Developer` изменяет
workspace через MCP, а `DeveloperVerifier` только проверяет состояние и запускает
проверки. Результаты объединяет `DeveloperResultFormatter`, у которого нет
инструментов.

Developer workflow разделён на два этапа:

1. `Developer` работает с MCP tools и выполняет действия в workspace.
2. `DeveloperResultFormatter` без tools формирует typed `ImplementationResult` из action transcript.

Ограничения action stage:

- максимум 20 итераций function invocation на один запрос;
- timeout 90 секунд;
- typed result не является доказательством изменения workspace;
- governance по-прежнему требует `ChangedFiles`, `WorkspaceRevision`, `DiffHash` и `ToolCalls` для `Implemented=true`.

Разделение нужно потому, что free-модели могут корректно выполнять tool calls, но нестабильно совмещают tool call и structured JSON в одном ответе. MCP используется только в action stage; formatter работает без MCP-инструментов.

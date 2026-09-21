# Developer action loop

The action loop uses separate agents with enforced MCP allow-lists: `DeveloperExplorer` is read-only, `DeveloperImplementer` can apply patches, and `DeveloperVerifier` can only inspect and run checks. Their outputs are combined by the tool-free `DeveloperResultFormatter`.

Developer workflow разделён на два этапа:

1. `Developer` работает с MCP tools и выполняет действия в workspace.
2. `DeveloperResultFormatter` без tools формирует typed `ImplementationResult` из action transcript.

Ограничения action stage:

- максимум 20 итераций function invocation на один запрос;
- timeout 90 секунд;
- typed result не является доказательством изменения workspace;
- governance по-прежнему требует `ChangedFiles`, `WorkspaceRevision`, `DiffHash` и `ToolCalls` для `Implemented=true`.

Разделение нужно потому, что free-модели могут корректно выполнять tool calls, но нестабильно совмещают tool call и structured JSON в одном ответе. MCP используется только в action stage; formatter работает без MCP-инструментов.

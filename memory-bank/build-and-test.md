# Сборка и тесты

Базовые команды из каталога workspace:

```text
dotnet restore workspace.slnx
dotnet build workspace.slnx
dotnet test workspace.slnx
```

Тестовый проект `tests/Workspace.Tests` использует NUnit. E2E-тест помечен
`Explicit`, потому что требует запущенный Ollama и модель с tool calling.

## Проверки по типу изменения

- Всегда: `dotnet restore workspace.slnx` и `dotnet build workspace.slnx`.
- MCP tool/transport: запустить `dotnet run --project src/McpServer`; stdout
  должен оставаться MCP-потоком, а discovery должен вернуть tools
  `get_workspace_status` и `echo`.
- AgentClient/workflow: при доступном Ollama выполнить
  `dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"` и
  проверить подключение MCP, typed workflow, correlation IDs и отсутствие
  недопустимого перехода.
- Контракты: проверить сериализацию нового record и producer/consumer.

Автоматические тесты:

- unit: `AgentClientOptions` и JSON/default stage typed contracts;
- integration: запуск реального `McpServer` и discovery/call `echo` через stdio;
- E2E: полный `AgentClient` workflow через MCP и Ollama, запуск отдельно:
  `dotnet test workspace.slnx --filter FullyQualifiedName~AgentWorkflowE2ETests`
  после установки `RUN_E2E_TESTS=true`.

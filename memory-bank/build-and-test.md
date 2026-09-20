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
- Для проверки Architect ↔ Security использовать сценарий
  `AgentClient_ReturnsSecurityFindingToArchitectForSuspiciousTokenLifetime` и
  убедиться, что trace содержит `transition:Security->Architect`.
- Для Short URL: `dotnet test workspace.slnx` проверяет application service и
  HTTP API на InMemory provider; production-like persistence запускается через
  `docker compose up --build short-url postgres redis`.
- OpenAI E2E запускается отдельно с `RUN_OPENAI_E2E=true` и
  `OPENAI_API_KEY`; он намеренно не входит в обязательный offline test suite.

- Developer/Tester loop: после неуспешного `TestReport` Manager передаёт
  Developer полный `DeveloperTesterFixRequest` с предыдущим
  `ImplementationResult` и findings Tester. В trace должны появляться
  `transition:Tester->Developer` и затем `transition:Developer->Tester`;
  число итераций ограничивается `WORKFLOW_MAX_CYCLES`.

- `ImplementationResult` с `Implemented=true` обязан содержать
  `ChangedFiles`, `WorkspaceRevision`, `DiffHash` и непустой `ToolCalls`;
  одного текстового `DiffSummary` недостаточно.

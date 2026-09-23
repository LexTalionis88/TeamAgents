# Сборка и тесты

Базовые команды из каталога workspace:

```text
dotnet restore workspace.slnx
dotnet build workspace.slnx
dotnet test workspace.slnx
```

При диагностике удалённого провайдера в OpenTelemetry следует проверить span
конкретного typed/action-вызова: `agent.request.input_chars`,
`agent.response.output_chars`, `agent.request.timeout_seconds`,
`agent.request.elapsed_ms` и `agent.request.succeeded`. При ошибке дополнительно
фиксируются `error.type`, `error.message` и статус span; при локальном исправлении
формата typed JSON — `agent.response.repair_attempted` и
`agent.response.initial_parse_error`. Для Groq capability адаптера ограничивают
размер ответа и включают компактные инструкции, поэтому результаты нужно
сравнивать с фактической квотой модели.

Тестовый проект `tests/Workspace.Tests` использует NUnit. E2E-тест помечен
`Explicit`, потому что требует запущенный Ollama и модель с tool calling.

## Проверки по типу изменения

- Всегда: `dotnet restore workspace.slnx` и `dotnet build workspace.slnx`.
- MCP tool/transport: запустить `dotnet run --project src/McpServer`; stdout
  должен оставаться MCP-потоком, а discovery должен вернуть инструменты статуса,
  файлов, patch, .NET-проверок и evidence.
- AgentClient/workflow: при доступном Ollama выполнить
  `dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"` и
  проверить подключение MCP, typed workflow, correlation IDs и отсутствие
  недопустимого перехода.
- Gemini E2E: задать `MODEL_PROVIDER=gemini`, `GEMINI_MODEL` и
  `GEMINI_API_KEY`, затем выполнить запуск AgentClient. В трассировке должны
  быть `gen_ai.request.model`, `generativelanguage.googleapis.com`, MCP
  `execute_tool` и workflow transitions. Бесплатный API может вернуть HTTP 429
  при превышении квоты.
- Groq E2E: задать `MODEL_PROVIDER=groq`, `GROQ_MODEL` и `GROQ_API_KEY`, затем
  выполнить тот же запуск AgentClient и проверить MCP tool calls и workflow
  transitions в OpenTelemetry. Для Groq typed JSON десериализуется локально:
  `response_format` не должен отправляться вместе с MCP tools.
- Tuzi E2E: задать `MODEL_PROVIDER=tuzi`, `TUZI_MODEL`,
  `TUZI_BASE_URL=https://api.tu-zi.com/v1` и `TUZI_API_KEY`, затем выполнить тот
  же запуск AgentClient. Модель должна поддерживать MCP tool calls и typed JSON;
  в trace нужно проверить `server.address=api.tu-zi.com`, `gen_ai.request.model`,
  `execute_tool`, workflow transitions и отсутствие секрета в логах.
- Cloudflare E2E: задать `MODEL_PROVIDER=cloudflare`,
  `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_API_TOKEN` и `CLOUDFLARE_MODEL`, затем
  выполнить тот же запуск AgentClient. Модель должна поддерживать MCP tool
  calls; в trace нужно проверить `server.address=api.cloudflare.com`,
  `gen_ai.request.model`, `execute_tool`, workflow transitions и отсутствие
  токена в логах. На бесплатной квоте Workers AI доступно 10 000 Neurons в сутки.
- Контракты: проверить сериализацию нового record и producer/consumer.
- Декомпозиция: проверить сериализацию `TaskPlan`/`WorkItem`, clamp
  `WORKFLOW_MAX_WORK_ITEMS`, отклонение монолитного плана для multi-concern
  задачи и переход Reviewer -> следующий WorkItem.
- Таймауты: проверить clamp `WORKFLOW_AGENT_TIMEOUT_SECONDS` и общего
  `WORKFLOW_TIMEOUT_SECONDS`, чтобы медленный provider не обрывал typed-вызов
  раньше установленного лимита.
- Read-only smoke: с `WORKFLOW_READ_ONLY=true` проверить, что агентам доступны
  только status, diff, evidence и чтение файлов, без patch/replace/check tools;
  отмена deadline должна завершать зависший MCP/Git subprocess.

Автоматические тесты:

- unit: `AgentClientOptions` и JSON/default stage typed contracts;
- integration: запуск реального `McpServer` и discovery/call `echo` через stdio;
- E2E: полный `AgentClient` workflow через MCP и Ollama, запуск отдельно:
  `dotnet test workspace.slnx --filter FullyQualifiedName~AgentWorkflowE2ETests`
  после установки `RUN_E2E_TESTS=true`.
- Для проверки Architect ↔ Security использовать сценарий
  `AgentClient_ReturnsSecurityFindingToArchitectForSuspiciousTokenLifetime` и
  убедиться, что trace содержит `transition:Security->Architect`.
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

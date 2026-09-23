# Наблюдаемость multi-agent workflow

## Граница задачи

Примеры в этом документе являются acceptance-сценариями, а не логикой workflow.
`EscalatingWorkflow` принимает произвольные .NET-требования и для каждой задачи
фиксирует одинаковые общие evidence этапов Explore -> Implement -> Verify ->
Test -> Security -> Review. В нём не должно быть bootstrap/repair-логики Short
URL, Like, хранилища, API-маршрутов или другого конкретного проекта.

## Назначение

Главный проверяемый результат workflow — автономное выполнение полноценной
.NET-задачи после однократной постановки требований. В качестве acceptance
scenario используются Short URL или Like service на ASP.NET Core с API,
PostgreSQL, Redis, Docker и tests. Пользователь не вмешивается между этапами:
Manager сам маршрутизирует задачу, Developer реализует её через MCP, а
Tester/Security/Reviewer запускают review/fix cycles и формируют доказуемый итог.

Актуальная реализация workflow начинается с Manager-планировщика, который
возвращает `TaskPlan` из небольших `WorkItem`. Затем для каждого среза работают
Architect, Developer, Tester, Security и Reviewer; после одобрения последнего
среза Manager final принимает общий результат. Между шагами используются
типизированные контракты; PolicyManager принимает решения только на gate-точках.

`src/AgentClient/Workflow/EscalatingWorkflow.cs` содержит workflow:

```text
запрос -> Manager(TaskPlan) -> Architect -> Developer(WorkItem) -> Tester
                                      -> Security -> Reviewer -> следующий WorkItem
                                      ^                 |
                                      +-- bounded fix/escalation
```

При findings или неоднозначности PolicyManager может вернуть workflow к
Architect или Developer. После одобрения одного WorkItem workflow не смешивает
его с остальными, а передаёт следующий срез отдельным Execute-этапом.
Допустимые переходы и лимит циклов проверяются кодом, а не только инструкциями модели.
Все агенты используют провайдер из
`MODEL_PROVIDER`; MCP tools discovery выполняется до запуска workflow. Для
Gemini typed JSON десериализуется локально из-за ограничений compatibility layer.

## Что наблюдать в консоли

Клиент одновременно выводит события workflow и экспортирует OpenTelemetry-трассы через `OpenTelemetry.Exporter.Console`.

- `workflow.build` — построенный граф и его определение.
- `workflow.session` и `workflow_invoke` — границы одного запуска workflow.
- `executor.process` — вход и результат конкретного executor-а.
- `edge_group.process` — переход по ребру; `delivered` показывает выбранную ветку, `dropped condition false` — отвергнутую условную ветку.
- `message.send` — передача сообщения между executor-ами.
- `invoke_agent` и `chat` — запуск агента и обращение к выбранному провайдеру.
- В `chat` видны `tool_call` и ответ MCP tool; это позволяет установить, какой инструмент был вызван.

Помимо трасс, строки `[WORKFLOW]` показывают `WorkflowStartedEvent`, `ExecutorInvokedEvent`, `ExecutorCompletedEvent`, `WorkflowOutputEvent` и завершение super-step.

Каждый typed/action-вызов агента дополнительно создаёт дочерний span
`agent.request` с именем агента, шагом, номером итерации, размером входа,
тайм-аутом, длительностью и размером ответа. При ошибке span получает статус
ошибки и тип исключения; если typed JSON пришлось исправлять локально,
фиксируются соответствующие признаки повторной попытки. В консольном логе
ошибка workflow также содержит `task_id`, `correlation_id` и имя провайдера.

## Воспроизведение

Для Ollama нужны локально запущенные Ollama и модель:

```text
ollama serve
ollama pull qwen3:1.7b
dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"
dotnet run --project src/AgentClient -- "Проанализируй текущий запрос"
```

Первый запрос проходит полный typed workflow и должен показать discovery MCP и
шаги Manager -> Architect -> Developer -> Tester -> Security -> Reviewer ->
Manager. Если модель вернула findings или неоднозначность, возможен возврат к
Architect/Developer до лимита циклов. Имя модели можно изменить через
`OLLAMA_MODEL`, endpoint — через `OLLAMA_HOST`.

Для Gemini:

```text
set MODEL_PROVIDER=gemini
set GEMINI_MODEL=gemini-3.8-flash
set GEMINI_API_KEY=...
dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"
```

Для Groq:

```text
set MODEL_PROVIDER=groq
set GROQ_MODEL=openai/gpt-oss-120b
set GROQ_API_KEY=...
dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"
```

Для Tuzi:

```text
set MODEL_PROVIDER=tuzi
set TUZI_MODEL=gpt-4.1-mini
set TUZI_BASE_URL=https://api.tu-zi.com/v1
set TUZI_API_KEY=...
dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"
```

Tuzi подключается как отдельный OpenAI-совместимый provider. `TUZI_API_KEY`
читается только из окружения; его нельзя включать в prompt, trace, README или
исходный код. Для E2E-проверки нужно подтвердить в trace фактические MCP
`execute_tool` и `gen_ai.request.model`, а не только успешный текстовый ответ.

Для Cloudflare Workers AI:

```text
set MODEL_PROVIDER=cloudflare
set CLOUDFLARE_ACCOUNT_ID=...
set CLOUDFLARE_API_TOKEN=...
set CLOUDFLARE_MODEL=@cf/ibm-granite/granite-4.0-h-micro
dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"
```

Cloudflare подключается через OpenAI-совместимый endpoint
`https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1`. Для
Cloudflare typed JSON разбирается локально, потому что JSON Mode не гарантирует
полное соответствие схеме. В trace нужно проверить
`server.address=api.cloudflare.com`, `gen_ai.request.model`, `execute_tool`,
workflow transitions и отсутствие токена в логах.

## Magentic режим

Для экспериментального динамического маршрута задайте:

```text
set WORKFLOW_ORCHESTRATION=magentic
```

В этом режиме `MagenticManager` выбирает участников и может вызывать Architect,
Developer, Tester, Security и Reviewer в любом порядке. Framework ограничивает число
rounds, stalls и resets. После завершения workflow приложение отдельно собирает MCP
diff/evidence, выполняет `dotnet test` в write-режиме и передаёт наблюдаемые данные
финальному typed Reviewer. Поэтому текстовый ответ Magentic сам по себе не считается
подтверждением изменения workspace.

По умолчанию остаётся `WORKFLOW_ORCHESTRATION=legacy`, чтобы сравнивать оба режима.

## Важные ограничения

В демонстрации включён `EnableSensitiveData`, поэтому в консоль попадают prompt, tool arguments и ответы модели. Это удобно для исследования порядка выполнения, но не должно включаться без оценки риска в production. Для безопасного режима отключите эту опцию в настройках workflow и агентов.

OpenTelemetry показывает причинно-следственную картину выполнения workflow. `dotnet-monitor` в Docker Compose решает другую задачу: сбор диагностических данных .NET-процесса через diagnostic socket. Он не заменяет workflow-трассировку.

Поддержка workflow и его spans реализована через `Microsoft.Agents.AI.Workflows` и `.WithOpenTelemetry(...)`; консольный вывод включён намеренно, чтобы пример работал без внешнего коллектора.

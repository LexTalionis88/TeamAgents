# Workspace MCP + Microsoft Agent Framework

Минимальный пример из двух приложений:

- `src/McpServer` — MCP Server на официальном C# SDK. Работает через stdio и
  предоставляет инструменты статуса, чтения и изменения workspace, запуска
  .NET-проверок и сбора evidence.
- `src/AgentClient` — консольный клиент на Microsoft Agent Framework. Он запускает
  MCP Server, подключает найденные MCP tools к агентам и проводит workflow:
  Manager сначала формирует `TaskPlan` из небольших `WorkItem`, затем каждый
  срез проходит Architect, Developer, Tester, Security и Reviewer.

## Запуск

По умолчанию используется совместимый `legacy` workflow. Для эксперимента с
Magentic orchestration задайте `WORKFLOW_ORCHESTRATION=magentic`; допустим также
алиас `magnetic`. В обоих режимах изменения workspace выполняются только через MCP,
а итог проверяется typed Reviewer по фактическому diff/evidence.

```text
dotnet restore workspace.slnx
dotnet build workspace.slnx
```

Для запуска клиента нужен запущенный Ollama и загруженная модель с поддержкой
вызова инструментов, например `qwen3:1.7b`:

```text
ollama pull qwen3:1.7b
set OLLAMA_HOST=http://localhost:11434
set OLLAMA_MODEL=qwen3:1.7b
dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"
```

Для Gemini задайте `MODEL_PROVIDER=gemini`, `GEMINI_MODEL=gemini-3.8-flash` и
`GEMINI_API_KEY`, затем запустите ту же команду. Ключ читается только из
переменных окружения. Gemini подключён через OpenAI-совместимый endpoint;
схемы JSON и строгие схемы функций адаптируются на стороне провайдера, а typed JSON
проверяется локальными правилами workflow.

Для Groq задайте `MODEL_PROVIDER=groq`, `GROQ_MODEL=openai/gpt-oss-120b` и
`GROQ_API_KEY`. Groq подключается через OpenAI-совместимый endpoint
`https://api.groq.com/openai/v1` и использует стандартный вызов инструментов
Agent Framework.

Для Tuzi задайте `MODEL_PROVIDER=tuzi`, точную модель через `TUZI_MODEL`,
`TUZI_BASE_URL=https://api.tu-zi.com/v1` и `TUZI_API_KEY`. Например:

```text
set MODEL_PROVIDER=tuzi
set TUZI_MODEL=gpt-4.1-mini
set TUZI_BASE_URL=https://api.tu-zi.com/v1
set TUZI_API_KEY=...
dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"
```

`TUZI_API_KEY` читается только из окружения и не должен попадать в исходники,
документацию или логи. Модель должна поддерживать вызовы инструментов и typed JSON.

Для Tuzi задайте `MODEL_PROVIDER=tuzi`, точную модель через `TUZI_MODEL`,
`TUZI_BASE_URL=https://api.tu-zi.com/v1` и `TUZI_API_KEY`. Например:

```text
set MODEL_PROVIDER=tuzi
set TUZI_MODEL=gpt-4.1-mini
set TUZI_BASE_URL=https://api.tu-zi.com/v1
set TUZI_API_KEY=...
dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"
```

`TUZI_API_KEY` читается только из окружения и не должен попадать в исходники,
документацию или логи. Модель должна поддерживать вызовы инструментов и typed JSON.

Для Cloudflare Workers AI задайте `MODEL_PROVIDER=cloudflare`,
`CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_API_TOKEN` и, при необходимости,
`CLOUDFLARE_MODEL`. По умолчанию используется
`@cf/ibm-granite/granite-4.0-h-micro`; endpoint строится из account ID. Для
нестандартного endpoint (например, AI Gateway) задайте `CLOUDFLARE_BASE_URL`.

```text
set MODEL_PROVIDER=cloudflare
set CLOUDFLARE_ACCOUNT_ID=...
set CLOUDFLARE_API_TOKEN=...
set CLOUDFLARE_MODEL=@cf/ibm-granite/granite-4.0-h-micro
dotnet run --project src/AgentClient -- "Проверь статус MCP-сервера"
```

Workers AI предоставляет OpenAI-совместимый Chat Completions endpoint и модели
с function calling. На бесплатном плане действует общая квота 10 000 Neurons в
сутки, а не постоянная бесплатность конкретной модели; квота сбрасывается в
00:00 UTC. Для текущего workflow подходят Granite 4.0 H-Micro (дешевле и с
function calling) и GPT-OSS 20B (function calling/reasoning, но дороже).
Токен должен иметь права Workers AI Read и Workers AI Edit.

Количество review/fix-циклов задаётся `WORKFLOW_MAX_CYCLES` (по умолчанию 2),
а максимальное число последовательных `WorkItem` —
`WORKFLOW_MAX_WORK_ITEMS` (по умолчанию 6). Таймаут одного typed-вызова агента
задаётся `WORKFLOW_AGENT_TIMEOUT_SECONDS` (по умолчанию 180 секунд). Общий
deadline запуска задаётся `WORKFLOW_TIMEOUT_SECONDS` (по умолчанию 900 секунд,
диапазон 60–3600); после его истечения отменяются AI-вызовы и MCP stdio-соединение.
Для диагностических задач включите `WORKFLOW_READ_ONLY=true`: Developer получает
только инструменты чтения, а workflow возвращает обзор без изменения workspace.

Клиент использует локальную интеграцию `OllamaSharp`; данные
обрабатываются локально.

Для запуска всего стека в Docker:

```text
ollama serve
ollama pull qwen3:1.7b
docker compose up -d monitor
docker compose up -d agent
```

Ollama работает на хосте, а контейнер AgentClient обращается к нему по адресу
`http://host.docker.internal:11434`. Если Ollama слушает только localhost и
Docker Desktop не может подключиться к нему, задайте для Ollama
`OLLAMA_HOST=0.0.0.0:11434` перед запуском `ollama serve`.

API `dotnet-monitor` доступен на `http://localhost:52324` и использует общий
diagnostic socket для наблюдения за .NET-процессом. Внешний порт можно изменить
через `DOTNET_MONITOR_PORT`.

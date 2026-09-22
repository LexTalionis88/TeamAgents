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

Количество review/fix-циклов задаётся `WORKFLOW_MAX_CYCLES` (по умолчанию 2),
а максимальное число последовательных `WorkItem` —
`WORKFLOW_MAX_WORK_ITEMS` (по умолчанию 6). Таймаут одного typed-вызова агента
задаётся `WORKFLOW_AGENT_TIMEOUT_SECONDS` (по умолчанию 180 секунд).

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

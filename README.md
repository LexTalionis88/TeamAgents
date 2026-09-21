# Workspace MCP + Microsoft Agent Framework

Минимальный пример из двух приложений:

- `src/McpServer` — MCP Server на официальном C# SDK. Работает через stdio и
  предоставляет инструменты `GetWorkspaceStatus` и `Echo`.
- `src/AgentClient` — консольный клиент на Microsoft Agent Framework. Он запускает
  MCP Server, подключает найденные MCP tools к агенту и отправляет запрос модели.

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
environment variables. Gemini подключён через OpenAI-compatible endpoint;
provider-side JSON Schema и strict function schemas адаптируются, а typed JSON
проверяется локально workflow governance.

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

# Обоснование NuGet-пакетов

Документ фиксирует причины выбора прямых зависимостей текущего технического
среза. Версии зафиксированы в `*.csproj`; при обновлении проверяй совместимость
с `.NET 10`, MCP-протоколом и локальным Ollama.

## Результат аудита безопасности

По состоянию на **18 сентября 2026 года** команда
`dotnet list workspace.slnx package --vulnerable --include-transitive` не нашла
известных уязвимостей в проектах `AgentClient` и `McpServer`. Команда
`dotnet list workspace.slnx package --outdated` также не показала доступных
обновлений из подключённых NuGet-источников.

Это результат текущего NuGet-аудита, а не гарантия отсутствия будущих CVE.
Проверку нужно повторять при изменении зависимостей и регулярно в CI.

## MCP Server (`src/McpServer`)

### `ModelContextProtocol` — `2.2.0`

Выбран официальный C# SDK Model Context Protocol. Он предоставляет серверную
регистрацию инструментов, stdio-транспорт и атрибуты обнаружения MCP tools.
Тот же пакет используется клиентом для подключения и вызова инструментов, что
сохраняет совместимый протокол между двумя проектами.

### `Microsoft.Extensions.Hosting` — `10.0.12`

Нужен для минимального host-процесса MCP-сервера, регистрации сервисов через DI и
управляемого жизненного цикла приложения. Это базовая инфраструктура .NET, а не
дополнительный AI-провайдер.

## Agent Client (`src/AgentClient`)

### `Microsoft.Agents.AI` — `1.21.0`

Это базовый пакет Microsoft Agent Framework для `AIAgent` и вызова агента.
Выбран отдельно от provider-пакетов, чтобы orchestration агента не зависела от
конкретного облачного сервиса.

### `OllamaSharp` — `5.4.30`

Предоставляет `OllamaApiClient`, реализующий `Microsoft.Extensions.AI.IChatClient`
и совместимый с `AIAgent`. Выбран потому, что inference должен выполняться
локально через Ollama, без облачного AI-провайдера. Это актуальная замена
deprecated-пакету `Microsoft.Extensions.AI.Ollama`, который больше не получает
обновлений и исправлений. При обновлении обязательно повторно проверять tool
calling выбранной модели.

### Почему `ModelContextProtocol` подключён и клиенту

`AgentClient` сам является MCP-клиентом: он запускает `McpServer` через stdio,
вызывает `ListToolsAsync()` и передаёт найденные tools агенту. Поэтому пакет
нужен в обоих проектах, а не только в сервере.

## Не выбранные зависимости

- Облачные AI-провайдеры и связанные credentials не добавлялись: inference
  выполняется локально через Ollama.
- RabbitMQ-клиент не добавлялся: RabbitMQ в текущей системе не реализован.
- ASP.NET Core не добавлялся: текущий MCP transport — stdio, HTTP host не нужен.
- Entity Framework и ORM не добавлялись: хранилище данных отсутствует.

## Tests (`tests/Workspace.Tests`)

### `NUnit` и `NUnit3TestAdapter`

Используются для unit, integration и explicit E2E тестов в одном test project.
Integration-тесты запускают реальный MCP stdio process; E2E-тест не выполняется
по умолчанию и требует Ollama, поэтому обычная проверка solution не зависит от
внешнего inference-сервиса.

### `Microsoft.NET.Test.Sdk`

Предоставляет test host для запуска NUnit через `dotnet test`.

## Правила обновления

Перед обновлением пакета:

1. проверить официальную документацию и release notes;
2. выполнить `dotnet restore workspace.slnx` и `dotnet build workspace.slnx`;
3. проверить MCP discovery и вызов tools;
4. проверить локальный вызов Ollama с моделью, поддерживающей tool calling;
5. обновить этот документ, если изменилась роль или причина выбора пакета.

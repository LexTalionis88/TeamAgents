# Обзор solution

## Основная цель проекта

Дать .NET-команде агентов полноценную задачу и после постановки требований не
вмешиваться в её выполнение. Эталонный сценарий — реализация Short URL или Like
service на ASP.NET Core с API, PostgreSQL, Redis, Docker и тестами.

В автономном прогоне Manager сам выбирает роли и маршрут, Developer изменяет
workspace через MCP, Tester/Security/Reviewer проводят проверки, а Manager
запускает ограниченные review/fix cycles и возвращает итог с подтверждёнными
изменёнными файлами, результатами команд, diff/evidence и trace. Участие
пользователя заканчивается на постановке требований.

Рабочая solution: [`workspace.slnx`](../workspace.slnx). В ней находятся два
консольных проекта: `src/McpServer` и `src/AgentClient`. Файл
[`workspace.sln`](../workspace.sln) оставлен для совместимости.

- `src/McpServer` — MCP-сервер со stdio-транспортом и инструментами статуса,
  файлов workspace, patch, .NET-проверок и evidence.
- `src/AgentClient` — клиент Microsoft Agent Framework с провайдером Ollama,
  OpenRouter, Gemini или Groq. Он запускает MCP-сервер, обнаруживает tools и
  проводит typed workflow через Manager, Architect, Developer, Tester, Security
  и Reviewer. Перед архитектурным этапом Manager разбивает требования в
  ограниченный `TaskPlan` из последовательных `WorkItem`.

Точка композиции клиента — `src/AgentClient/Application/AgentClientApplication.cs`;
маршрутизация и эскалации — `src/AgentClient/Workflow/EscalatingWorkflow.cs`;
typed records — `src/AgentClient/Contracts/WorkflowContracts.cs`.

- `tests/Workspace.Tests` — NUnit-тесты: unit, integration MCP/stdio и explicit
  E2E с Ollama или Gemini.

При добавлении проектов фиксируй здесь их назначение и основные зависимости.

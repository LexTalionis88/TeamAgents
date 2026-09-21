# Обзор solution

Рабочая solution: [`workspace.slnx`](../workspace.slnx). В ней находятся два
консольных проекта: `src/McpServer` и `src/AgentClient`. Файл
[`workspace.sln`](../workspace.sln) оставлен для совместимости.

- `src/McpServer` — MCP-сервер со stdio-транспортом и tools
  `get_workspace_status`/`echo`.
- `src/AgentClient` — клиент Microsoft Agent Framework с провайдером Ollama,
  OpenRouter или Gemini. Он запускает MCP-сервер, обнаруживает tools и проводит typed workflow
  через Manager, Architect, Developer, Tester, Security и Reviewer.

Точка композиции клиента — `src/AgentClient/Application/AgentClientApplication.cs`;
маршрутизация и эскалации — `src/AgentClient/Workflow/EscalatingWorkflow.cs`;
typed records — `src/AgentClient/Contracts/WorkflowContracts.cs`.

- `tests/Workspace.Tests` — NUnit-тесты: unit, integration MCP/stdio и explicit
  E2E с Ollama или Gemini.

При добавлении проектов фиксируй здесь их назначение и основные зависимости.

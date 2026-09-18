# Обзор solution

Рабочая solution: [`workspace.slnx`](../workspace.slnx). В ней находятся два
консольных проекта: `src/McpServer` и `src/AgentClient`. Файл
[`workspace.sln`](../workspace.sln) оставлен для совместимости.

- `src/McpServer` — MCP-сервер со stdio-транспортом и инструментами workspace.
- `src/AgentClient` — клиент Microsoft Agent Framework с локальной моделью Ollama,
  который запускает MCP-сервер и передаёт ему вызовы инструментов.

При добавлении проектов фиксируй здесь их назначение и основные зависимости.

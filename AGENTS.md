# AGENTS.md

Краткий маршрутизатор для работы с solution. Подробный контекст хранится в
документах memory bank и загружается только для соответствующего типа задачи.

## Базовые требования

- Поддерживаемая версия .NET: **.NET SDK 10.0.401** (зафиксирована в `global.json`).
- Основные команды из каталога solution:

  ```text
  dotnet restore
  dotnet build
  dotnet test
  ```

## Карта solution

- Solution: [`workspace.slnx`](workspace.slnx); [`workspace.sln`](workspace.sln)
  оставлена для совместимости.
- Проекты и назначение: см. **[обзор solution](memory-bank/solution-overview.md)**.

## Навигация по memory bank

- Архитектурные изменения: **[architecture.md](memory-bank/architecture.md)**.
- Изменения модулей и функциональности: **[modules.md](memory-bank/modules.md)**.
- Изменения API и контрактов: **[api.md](memory-bank/api.md)**.
- Изменения сборки, тестов и CI: **[build-and-test.md](memory-bank/build-and-test.md)**.
- Термины и доменные инварианты: **[glossary-and-invariants.md](docs/ai/glossary-and-invariants.md)**.
- RabbitMQ и messaging: **[rabbitmq.md](docs/ai/modules/rabbitmq.md)**.
- Обоснование NuGet-пакетов: **[nuget-packages.md](docs/ai/decisions/nuget-packages.md)**.
- Observability Agent Framework: **[agent-workflow.md](docs/ai/observability/agent-workflow.md)**.
- Typed-контракты агентов: **[typed-contracts.md](docs/ai/observability/typed-contracts.md)**.
- Correlation и metadata workflow: **[correlation.md](docs/ai/observability/correlation.md)**.
- Пример разбора trace: **[trace-review.md](docs/ai/observability/trace-review.md)**.
- Правила эскалации workflow: **[escalation.md](docs/ai/observability/escalation.md)**.

## Обязательный алгоритм новой coding-agent-сессии

Перед изменением кода агент должен:

1. Прочитать этот файл и `memory-bank/solution-overview.md`.
2. По типу задачи открыть документы из таблицы ниже; при изменении workflow
   дополнительно открыть `architecture.md`, `modules.md`,
   `glossary-and-invariants.md`, `agent-workflow.md`, `typed-contracts.md` и
   `escalation.md`.
3. До редактирования назвать затрагиваемые проекты/слои, проверить границы
   зависимостей и выписать применимые инварианты.
4. Найти исходные файлы через карту компонентов в `modules.md` и
   `architecture.md`, а не предполагать расположение кода.
5. Предложить тесты, относящиеся к изменению, и после изменения выполнить
   доступные проверки из `build-and-test.md`.
6. Если меняются архитектура, API, workflow-контракты или способ сборки,
   обновить соответствующий memory bank в том же изменении.

| Задача | Обязательные документы |
|---|---|
| MCP tool, stdio или сервер | `solution-overview.md`, `modules.md`, `architecture.md`, `api.md`, `glossary-and-invariants.md`, `build-and-test.md` |
| AgentClient, агент или workflow | `solution-overview.md`, `modules.md`, `architecture.md`, `typed-contracts.md`, `agent-workflow.md`, `correlation.md`, `escalation.md`, `glossary-and-invariants.md`, `build-and-test.md` |
| Конфигурация, запуск, Docker или зависимости | `solution-overview.md`, `architecture.md`, `build-and-test.md`, `nuget-packages.md` и `README.md` |
| RabbitMQ | `modules.md`, `architecture.md`, `rabbitmq.md`, `glossary-and-invariants.md`, `build-and-test.md` |

Если задача затрагивает публичный MCP-инструмент или typed record, это нужно
считать изменением контракта и проверять совместимость вызывающей стороны.

При изменении архитектуры обновляй соответствующий документ memory bank в том же
изменении.

## Запрещённые и опасные действия

- Не выполнять `git reset --hard`, `git checkout --`, удаление файлов или
  переписывание истории без явного указания пользователя.
- Не изменять публичные контракты, конфигурацию окружения или зависимости без
  проверки последствий.
- Не коммитить секреты, ключи, токены и локальные файлы с чувствительными данными.
- Не обходить ошибки сборки или тестов отключением проверок; сначала выяснить
  причину.

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

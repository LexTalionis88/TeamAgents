# Пакеты для наблюдаемости Agent Framework

## `Microsoft.Agents.AI.Workflows` — `1.21.0`

Пакет нужен для исполняемого графа multi-agent workflow, условных переходов между executor-ами, потоковых событий выполнения и встроенной OpenTelemetry-инструментации Microsoft Agent Framework.

## `OpenTelemetry.Exporter.Console` — `1.12.0`

Пакет выбран для локального просмотра spans без отдельного OpenTelemetry Collector. Клиент создаёт `TracerProvider` напрямую, поэтому `OpenTelemetry.Extensions.Hosting` не используется и не входит в проект.

## Правило выбора

Зависимости наблюдаемости должны оставаться минимальными: workflow-пакет отвечает за семантику Agent Framework, а console exporter — только за вывод трасс. Для production можно заменить exporter на OTLP, не меняя граф workflow.

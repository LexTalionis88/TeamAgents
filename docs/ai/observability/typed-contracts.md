# Typed-контракты между агентами

## Цепочка

Текущий workflow в \`src/AgentClient/Program.cs\` передаёт данные между агентами только через типизированные records:

\`\`\`text
ArchitectureQuestion
  -> ArchitectureDecision
  -> ImplementationResult
  -> TestReport
  -> SecurityReview
  -> ReviewResult
\`\`\`

Каждый переход является generic edge с типом результата предыдущего executor-а. Поэтому компилятор не позволяет подключить агент с несовместимым входом, а OpenTelemetry показывает тип в \`executor.type\` и \`message.type\`.

## Контракты

| Контракт | Роль |
|---|---|
| \`ArchitectureQuestion\` | исходный вопрос, контекст и ограничения |
| \`ArchitectureDecision\` | архитектурное решение и затронутые области |
| \`ImplementationResult\` | результат реализации и оставшаяся работа |
| \`TestReport\` | проверки, ошибки и рекомендации |
| \`SecurityReview\` | findings, риски и обязательные действия |
| \`ReviewResult\` | итоговое решение, блокирующие проблемы и следующие шаги |

## Structured output

Каждый агент вызывается через \`AIAgent.RunAsync<T>\`. Framework запрашивает JSON-схему по generic-типу и десериализует результат в \`AgentResponse<T>\`. Свободный текст не используется как межагентный контракт.

Локальная модель Ollama должна поддерживать structured output. Если модель вернула JSON, который не соответствует схеме, workflow завершается ошибкой десериализации, а не передаёт неопределённый текст дальше.

В текущем демонстрационном режиме включён \`EnableSensitiveData\`, поэтому в консольных spans видны входные и выходные JSON. Перед production-использованием это следует отключить или настроить redaction.

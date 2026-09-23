# Выбор модели Cloudflare Workers AI для agent workflow

На 23 сентября 2026 года Cloudflare Workers AI предоставляет OpenAI-совместимый
endpoint `https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1`.
Для вызова нужен API token и account ID; модель передаётся в поле `model`.

## Рекомендуемый default

В `CLOUDFLARE_MODEL` по умолчанию используется
`@cf/ibm-granite/granite-4.0-h-micro`. Официальный каталог Cloudflare помечает
модель как поддерживающую function calling и описывает её как подходящую для
multi-agent workflows. Это важно для передачи MCP tools через Agent Framework.

`CloudflareChatClientProvider` локально десериализует typed JSON: документация
Cloudflare предупреждает, что JSON Mode не гарантирует точное соответствие
запрошенной JSON Schema. При этом стандартный tool calling остаётся включённым.

## Бесплатная квота

Workers AI не предоставляет отдельную навсегда бесплатную модель. На Free и
Paid Workers действует общая бесплатная квота 10 000 Neurons в сутки; после
превышения на Free дальнейшие операции не выполняются, а на Paid превышение
тарифицируется. Лимит сбрасывается в 00:00 UTC.

Модели, пригодные для проверки agent workflow:

- `@cf/ibm-granite/granite-4.0-h-micro` — function calling, низкая стоимость;
- `@cf/openai/gpt-oss-20b` — function calling и reasoning, но дороже Granite.

Это модели в пределах общей бесплатной квоты, а не бессрочно бесплатные
эндпоинты. Frontier-модели, перечисленные Cloudflare как требующие paid billing
method, для бесплатного режима не подходят.

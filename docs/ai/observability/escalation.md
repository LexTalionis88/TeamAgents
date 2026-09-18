# Эскалации и неопределённость workflow

## Правила

- Developer не может менять требования или архитектуру молча. Если изменение необходимо или вход неоднозначен, он должен вернуть NeedsClarification=true и ArchitectureQuestion.
- Tester возвращает findings и failures. При RequiresEscalation=true Manager решает, нужен ли повторный Developer.
- Security возвращает findings, risks и required actions. При RequiresEscalation=true Manager решает, нужен ли повторный Developer.
- Manager policy возвращает ManagerDecision с допустимым следующим участником и причиной.

## Защита маршрута

Orchestrator проверяет допустимые переходы:

- Developer gate: Architect, Developer или Tester;
- Tester gate: Developer или Security;
- Security gate: Developer или Reviewer.

Любой другой маршрут, включая имя MCP tool, немедленно завершает workflow ошибкой. Policy Manager не получает MCP tools, чтобы tool names не смешивались с routing contract.

## Ограничение циклов

Лимит задаётся переменной WORKFLOW_MAX_CYCLES и по умолчанию равен 2. Значение ограничивается диапазоном 0–3. После превышения лимита workflow завершается ошибкой с указанием причины; бесконечный retry невозможен.

Каждый повторный шаг получает увеличенный iteration и trace transition span с from.agent, to.agent и manager.reason.

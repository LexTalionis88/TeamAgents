# Эскалации и неопределённость workflow

## Правила

- Developer не может менять требования или архитектуру молча. Если изменение необходимо или вход неоднозначен, он должен вернуть NeedsClarification=true и ArchitectureQuestion.
- Tester возвращает findings и failures. При RequiresEscalation=true Manager решает, нужен ли повторный Developer.
- Reviewer получает implementation, TestReport и SecurityReview. При blocking
  finding Manager передаёт Developer `DeveloperReviewerFixRequest`; после
  исправления workflow повторяет Tester и Reviewer до лимита итераций.
- Security получает текущий `ArchitectureDecision` и `TestReport`, возвращает
  findings, risks и required actions. При `ArchitectureChallenged=true` и
  `RequiresEscalation=true` Manager может вернуть finding Architect; Architect
  получает `ArchitectureRevisionRequest` и выпускает новый
  `ArchitectureDecision`. Иначе Manager выбирает повторный Developer или
  Reviewer.
- Manager policy возвращает ManagerDecision с допустимым следующим участником и причиной.

## Защита маршрута

Orchestrator проверяет допустимые переходы:

- Developer gate: Architect, Developer или Tester;
- Tester gate: Developer или Security;
- Security gate: Architect, Developer или Reviewer; Architect разрешён только
  при `ArchitectureChallenged=true`.

Любой другой маршрут, включая имя MCP tool, немедленно завершает workflow ошибкой. Policy Manager не получает MCP tools, чтобы tool names не смешивались с routing contract.

## Ограничение циклов

Лимит задаётся переменной WORKFLOW_MAX_CYCLES и по умолчанию равен 2. Значение ограничивается диапазоном 0–3. После превышения лимита workflow завершается ошибкой с указанием причины; бесконечный retry невозможен.

Каждый повторный шаг получает увеличенный iteration и trace transition span с from.agent, to.agent и manager.reason.

## Сценарий auth/token lifetime

Для сомнительной схемы с долгоживущим access token и отсутствующей ротацией
refresh token ожидается:

```text
Security(ArchitectureChallenged=true)
  -> Manager policy
  -> Architect(ArchitectureRevisionRequest)
  -> новый ArchitectureDecision
  -> Developer
```

Проверка запускается explicit NUnit E2E-тестом
`AgentClient_ReturnsSecurityFindingToArchitectForSuspiciousTokenLifetime`.

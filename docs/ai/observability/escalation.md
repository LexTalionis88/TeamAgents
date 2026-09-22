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
- Manager decomposition возвращает `TaskPlan`. WorkItem считается завершённым
  только после Reviewer; затем workflow явно переходит к следующему WorkItem.
- Для постановки с несколькими независимыми concern-группами validator требует
  минимум два или три WorkItem. Один монолитный item не проходит correction-gate;
  при этом сам workflow не знает предметную область и использует только общие
  признаки API, данных, инфраструктуры, тестов и документации.

## Защита маршрута

Orchestrator проверяет допустимые переходы:

- Developer gate: Architect, Developer или Tester;
- Tester gate: Developer или Security;
- Security gate: Architect, Developer или Reviewer; Architect разрешён только
  при `ArchitectureChallenged=true`.

Любой другой маршрут, включая имя MCP tool, немедленно завершает workflow ошибкой. Policy Manager не получает MCP tools, чтобы tool names не смешивались с routing contract.

## Ограничение циклов

Лимит задаётся переменной WORKFLOW_MAX_CYCLES и по умолчанию равен 2. Значение ограничивается диапазоном 0–3. После превышения лимита workflow завершается ошибкой с указанием причины; бесконечный retry невозможен.

Количество WorkItem ограничивается переменной `WORKFLOW_MAX_WORK_ITEMS` и по
умолчанию равно 6. План не может превышать этот лимит, а один WorkItem не может
обойти собственные Tester/Security/Reviewer-gates.

Каждый повторный шаг получает увеличенный iteration и trace transition span с from.agent, to.agent и manager.reason.

Таймаут одного typed-вызова агента задаётся `WORKFLOW_AGENT_TIMEOUT_SECONDS` и
по умолчанию равен 180 секундам; значение ограничивается диапазоном 30–600.

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

namespace AgentClient;

/// <summary>
/// Один независимый срез исходной задачи, который можно реализовать и проверить
/// отдельно от остальных срезов.
/// </summary>
/// <param name="Id">Стабильный идентификатор среза внутри плана.</param>
/// <param name="Title">Краткое название среза.</param>
/// <param name="Objective">Результат, который должен появиться после выполнения среза.</param>
/// <param name="AcceptanceCriteria">Проверяемые критерии готовности среза.</param>
/// <param name="Dependencies">Идентификаторы срезов, которые должны быть выполнены раньше.</param>
public sealed record WorkItem(
    string Id,
    string Title,
    string Objective,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> Dependencies);

/// <summary>
/// План декомпозиции исходных требований, подготовленный Manager.
/// </summary>
/// <param name="Summary">Краткое описание стратегии разбиения задачи.</param>
/// <param name="Items">Последовательность независимых реализуемых срезов.</param>
/// <param name="UnresolvedRequirements">Требования, которые нельзя потерять при реализации.</param>
public sealed record TaskPlan(
    string Summary,
    IReadOnlyList<WorkItem> Items,
    IReadOnlyList<string> UnresolvedRequirements);

/// <summary>
/// Исходные требования пользователя, передаваемые архитектору и Developer.
/// Контракт не содержит предметной логики и описывает только постановку задачи.
/// </summary>
/// <param name="Question">Текст задачи, который нельзя менять без явного согласования.</param>
/// <param name="Context">Контекст репозитория, окружения и предыдущих наблюдений.</param>
/// <param name="Constraints">Ограничения, обязательные для всех этапов workflow.</param>
public sealed record ArchitectureQuestion(
    string Question,
    string Context,
    IReadOnlyList<string> Constraints);

/// <summary>
/// Архитектурное решение, принятое Architect для исходных требований.
/// </summary>
/// <param name="Summary">Краткое описание принятого решения.</param>
/// <param name="Decisions">Конкретные архитектурные решения.</param>
/// <param name="TradeOffs">Осознанные компромиссы и ограничения выбранного подхода.</param>
/// <param name="AffectedAreas">Области workspace, которых касается решение.</param>
/// <param name="RequirementsAccepted">Приняты ли исходные требования без изменений.</param>
/// <param name="ChangedRequirements">Изменения требований, требующие отдельного согласования.</param>
public sealed record ArchitectureDecision(
    string Summary,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<string> TradeOffs,
    IReadOnlyList<string> AffectedAreas,
    bool RequirementsAccepted,
    IReadOnlyList<string> ChangedRequirements);

/// <summary>
/// Подтверждённый результат работы Developer с данными workspace evidence.
/// </summary>
/// <param name="Implemented">Подтверждено ли выполнение требуемой реализации.</param>
/// <param name="Summary">Краткое описание фактически выполненной работы.</param>
/// <param name="ChangedAreas">Изменённые логические области решения.</param>
/// <param name="RemainingWork">Работа, которая осталась после текущего этапа.</param>
/// <param name="NeedsClarification">Требуется ли архитектурное уточнение через Manager.</param>
/// <param name="ArchitectureQuestion">Уточнённые требования, если требуется пересмотр архитектуры.</param>
/// <param name="RequirementsChanged">Изменялись ли исходные требования.</param>
/// <param name="ArchitectureChanged">Изменялась ли согласованная архитектура.</param>
/// <param name="ChangedFiles">Файлы, подтверждённо изменённые в workspace.</param>
/// <param name="CommandsRun">Команды проверок, реально выполненные через MCP.</param>
/// <param name="BuildPassed">Успешна ли сборка.</param>
/// <param name="TestsPassed">Успешны ли тесты.</param>
/// <param name="DiffSummary">Краткое описание наблюдаемого diff.</param>
/// <param name="WorkspaceRevision">Ревизия workspace, зафиксированная evidence.</param>
/// <param name="DiffHash">Хеш наблюдаемого diff.</param>
/// <param name="ToolCalls">MCP-инструменты, реально вызванные в ходе работы.</param>
public sealed record ImplementationResult(
    bool Implemented,
    string Summary,
    IReadOnlyList<string> ChangedAreas,
    IReadOnlyList<string> RemainingWork,
    bool NeedsClarification,
    ArchitectureQuestion? ArchitectureQuestion,
    bool RequirementsChanged,
    bool ArchitectureChanged,
    IReadOnlyList<string>? ChangedFiles = null,
    IReadOnlyList<string>? CommandsRun = null,
    bool BuildPassed = false,
    bool TestsPassed = false,
    string? DiffSummary = null,
    string? WorkspaceRevision = null,
    string? DiffHash = null,
    IReadOnlyList<string>? ToolCalls = null);

/// <summary>
/// Устаревший контракт полного patch, сохранённый для совместимости форматов.
/// Текущий Developer должен изменять workspace непосредственно через MCP.
/// </summary>
/// <param name="Patch">Полный patch, если он используется совместимым вызывающим кодом.</param>
/// <param name="Summary">Описание изменений, содержащихся в patch.</param>
internal sealed record ImplementationPatch(
    string Patch,
    string Summary);

/// <summary>
/// Отчёт Tester о результатах автоматических и ручных проверок workspace.
/// </summary>
/// <param name="Passed">Пройдены ли обязательные проверки.</param>
/// <param name="Checks">Проверки, которые были выполнены.</param>
/// <param name="Findings">Обнаруженные проблемы, не обязательно блокирующие релиз.</param>
/// <param name="Failures">Проверки, завершившиеся ошибкой.</param>
/// <param name="Recommendations">Рекомендации по дальнейшим действиям.</param>
/// <param name="RequiresEscalation">Нужно ли передать решение следующему gate workflow.</param>
public sealed record TestReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Recommendations,
    bool RequiresEscalation);

/// <summary>
/// Отчёт Security о рисках и необходимых защитных мерах.
/// </summary>
/// <param name="Passed">Пройдена ли проверка безопасности.</param>
/// <param name="Findings">Конкретные найденные проблемы безопасности.</param>
/// <param name="Risks">Оценённые риски для реализации и окружения.</param>
/// <param name="RequiredActions">Действия, необходимые для устранения рисков.</param>
/// <param name="RequiresEscalation">Нужно ли продолжить эскалацию через Manager.</param>
/// <param name="ArchitectureChallenged">Требует ли finding пересмотра архитектуры.</param>
public sealed record SecurityReview(
    bool Passed,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> RequiredActions,
    bool RequiresEscalation,
    bool ArchitectureChallenged);

/// <summary>
/// Итоговая оценка Reviewer после прохождения всех gate-проверок.
/// </summary>
/// <param name="Approved">Одобрена ли текущая реализация.</param>
/// <param name="Summary">Краткое обоснование итоговой оценки.</param>
/// <param name="BlockingIssues">Проблемы, блокирующие одобрение.</param>
/// <param name="NextSteps">Рекомендуемые следующие шаги.</param>
public sealed record ReviewResult(
    bool Approved,
    string Summary,
    IReadOnlyList<string> BlockingIssues,
    IReadOnlyList<string> NextSteps);

/// <summary>
/// Решение Manager о следующем этапе типизированного workflow.
/// </summary>
/// <param name="NextAgent">Имя роли, которая должна выполняться следующей.</param>
/// <param name="Reason">Причина выбранного маршрута.</param>
/// <param name="Cycle">Номер текущего bounded-цикла.</param>
/// <param name="RequirementsAccepted">Приняты ли требования без несанкционированных изменений.</param>
/// <param name="ArchitectureAccepted">Принята ли текущая архитектура.</param>
/// <param name="ArchitectureQuestion">Новые требования при разрешённой эскалации к Architect.</param>
public sealed record ManagerDecision(
    string NextAgent,
    string Reason,
    int Cycle,
    bool RequirementsAccepted,
    bool ArchitectureAccepted,
    ArchitectureQuestion? ArchitectureQuestion);

/// <summary>
/// Вход для policy-gate после завершения Developer.
/// </summary>
/// <param name="Architecture">Текущее архитектурное решение.</param>
/// <param name="Implementation">Результат реализации, который требуется маршрутизировать.</param>
/// <param name="Cycle">Номер текущего цикла.</param>
/// <param name="Stage">Имя workflow-этапа, сформировавшего вход.</param>
public sealed record DeveloperEscalationInput(
    ArchitectureDecision Architecture,
    ImplementationResult Implementation,
    int Cycle,
    string Stage = "Developer");

/// <summary>
/// Вход для policy-gate после отчёта Tester.
/// </summary>
/// <param name="Implementation">Текущий результат реализации.</param>
/// <param name="Tests">Отчёт Tester.</param>
/// <param name="Cycle">Номер текущего цикла.</param>
/// <param name="Stage">Имя workflow-этапа, сформировавшего вход.</param>
public sealed record TestEscalationInput(
    ImplementationResult Implementation,
    TestReport Tests,
    int Cycle,
    string Stage = "Tester");

/// <summary>
/// Запрос Developer на исправление замечаний Tester.
/// </summary>
/// <param name="Architecture">Архитектура, в рамках которой выполняется исправление.</param>
/// <param name="PreviousImplementation">Предыдущий подтверждённый результат реализации.</param>
/// <param name="TesterReport">Замечания и результаты проверки Tester.</param>
/// <param name="Cycle">Номер цикла исправления.</param>
/// <param name="Stage">Имя этапа, сформировавшего запрос.</param>
public sealed record DeveloperTesterFixRequest(
    ArchitectureDecision Architecture,
    ImplementationResult PreviousImplementation,
    TestReport TesterReport,
    int Cycle,
    string Stage = "ManagerDeveloperFix");

/// <summary>
/// Запрос Developer на исправление блокирующих замечаний Reviewer.
/// </summary>
/// <param name="Architecture">Архитектура, в рамках которой выполняется исправление.</param>
/// <param name="PreviousImplementation">Предыдущий подтверждённый результат реализации.</param>
/// <param name="ReviewerReport">Итоговые замечания Reviewer.</param>
/// <param name="Cycle">Номер цикла исправления.</param>
/// <param name="Stage">Имя этапа, сформировавшего запрос.</param>
public sealed record DeveloperReviewerFixRequest(
    ArchitectureDecision Architecture,
    ImplementationResult PreviousImplementation,
    ReviewResult ReviewerReport,
    int Cycle,
    string Stage = "ManagerDeveloperReviewFix");

/// <summary>
/// Вход Security для анализа реализации после тестирования.
/// </summary>
/// <param name="Architecture">Текущее архитектурное решение.</param>
/// <param name="Tests">Отчёт Tester, используемый как часть контекста проверки.</param>
/// <param name="Cycle">Номер текущего цикла.</param>
/// <param name="Stage">Имя workflow-этапа.</param>
public sealed record SecurityReviewInput(
    ArchitectureDecision Architecture,
    TestReport Tests,
    int Cycle,
    string Stage = "Security");

/// <summary>
/// Вход для policy-gate после проверки Security.
/// </summary>
/// <param name="Architecture">Текущее архитектурное решение.</param>
/// <param name="Tests">Отчёт Tester.</param>
/// <param name="Security">Отчёт Security.</param>
/// <param name="Cycle">Номер текущего цикла.</param>
/// <param name="Stage">Имя workflow-этапа.</param>
public sealed record SecurityEscalationInput(
    ArchitectureDecision Architecture,
    TestReport Tests,
    SecurityReview Security,
    int Cycle,
    string Stage = "Security");

/// <summary>
/// Вход Reviewer для проверки совокупного результата workflow.
/// </summary>
/// <param name="Architecture">Архитектурное решение.</param>
/// <param name="Implementation">Результат реализации.</param>
/// <param name="Tests">Результаты тестирования.</param>
/// <param name="Security">Результаты проверки безопасности.</param>
/// <param name="Cycle">Номер текущего цикла.</param>
/// <param name="Stage">Имя workflow-этапа.</param>
public sealed record ReviewerInput(
    ArchitectureDecision Architecture,
    ImplementationResult Implementation,
    TestReport Tests,
    SecurityReview Security,
    int Cycle,
    string Stage = "Reviewer");

/// <summary>
/// Вход для policy-gate после итогового заключения Reviewer.
/// </summary>
/// <param name="ReviewInput">Контекст, на основании которого выполнено ревью.</param>
/// <param name="Review">Итоговый отчёт Reviewer.</param>
/// <param name="Cycle">Номер текущего цикла.</param>
/// <param name="Stage">Имя workflow-этапа.</param>
public sealed record ReviewEscalationInput(
    ReviewerInput ReviewInput,
    ReviewResult Review,
    int Cycle,
    string Stage = "Reviewer");

/// <summary>
/// Запрос Architect на пересмотр решения после существенного замечания Security.
/// </summary>
/// <param name="CurrentArchitecture">Архитектура до пересмотра.</param>
/// <param name="Security">Отчёт, содержащий архитектурное возражение.</param>
/// <param name="Cycle">Номер текущего цикла.</param>
/// <param name="Stage">Причина и источник пересмотра.</param>
public sealed record ArchitectureRevisionRequest(
    ArchitectureDecision CurrentArchitecture,
    SecurityReview Security,
    int Cycle,
    string Stage = "SecurityArchitectureChallenge");

/// <summary>
/// Запрос governance на исправление формата или содержания ArchitectureDecision.
/// </summary>
/// <param name="InvalidArchitecture">Ответ Architect, не прошедший проверку контракта.</param>
/// <param name="Reason">Причина, которую нужно устранить без изменения требований.</param>
/// <param name="Cycle">Номер текущего цикла.</param>
/// <param name="Stage">Имя этапа governance.</param>
public sealed record ArchitectureCorrectionRequest(
    ArchitectureDecision InvalidArchitecture,
    string Reason,
    int Cycle,
    string Stage = "ArchitectureGovernanceCorrection");

/// <summary>
/// Запрос governance на повторную реализацию с подтверждением workspace evidence.
/// </summary>
/// <param name="Architecture">Архитектура, обязательная для исправления.</param>
/// <param name="InvalidImplementation">Предыдущий результат, не прошедший governance.</param>
/// <param name="Reason">Причина возврата Developer на исправление.</param>
/// <param name="Cycle">Номер текущего цикла.</param>
/// <param name="Stage">Имя этапа governance.</param>
public sealed record ImplementationCorrectionRequest(
    ArchitectureDecision Architecture,
    ImplementationResult InvalidImplementation,
    string Reason,
    int Cycle,
    string Stage = "ImplementationGovernanceCorrection");

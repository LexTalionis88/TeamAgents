namespace AgentClient;

/// <summary>Вопрос, который запускает архитектурный workflow.</summary>
public sealed record ArchitectureQuestion(
    string Question,
    string Context,
    IReadOnlyList<string> Constraints);

/// <summary>Решение по архитектуре, передаваемое на реализацию.</summary>
public sealed record ArchitectureDecision(
    string Summary,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<string> TradeOffs,
    IReadOnlyList<string> AffectedAreas);

/// <summary>Результат предполагаемой реализации архитектурного решения.</summary>
public sealed record ImplementationResult(
    bool Implemented,
    string Summary,
    IReadOnlyList<string> ChangedAreas,
    IReadOnlyList<string> RemainingWork);

/// <summary>Отчёт о проверках реализации.</summary>
public sealed record TestReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Recommendations);

/// <summary>Результат проверки безопасности.</summary>
public sealed record SecurityReview(
    bool Passed,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> RequiredActions);

/// <summary>Итог ревью всего workflow.</summary>
public sealed record ReviewResult(
    bool Approved,
    string Summary,
    IReadOnlyList<string> BlockingIssues,
    IReadOnlyList<string> NextSteps);

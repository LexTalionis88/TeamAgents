namespace AgentClient;

public sealed record ArchitectureQuestion(
    string Question,
    string Context,
    IReadOnlyList<string> Constraints);

public sealed record ArchitectureDecision(
    string Summary,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<string> TradeOffs,
    IReadOnlyList<string> AffectedAreas,
    bool RequirementsAccepted,
    IReadOnlyList<string> ChangedRequirements);

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
    string? DiffSummary = null);

public sealed record TestReport(
    bool Passed,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Recommendations,
    bool RequiresEscalation);

public sealed record SecurityReview(
    bool Passed,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> RequiredActions,
    bool RequiresEscalation,
    bool ArchitectureChallenged);

public sealed record ReviewResult(
    bool Approved,
    string Summary,
    IReadOnlyList<string> BlockingIssues,
    IReadOnlyList<string> NextSteps);

public sealed record ManagerDecision(
    string NextAgent,
    string Reason,
    int Cycle,
    bool RequirementsAccepted,
    bool ArchitectureAccepted,
    ArchitectureQuestion? ArchitectureQuestion);

public sealed record DeveloperEscalationInput(
    ArchitectureDecision Architecture,
    ImplementationResult Implementation,
    int Cycle,
    string Stage = "Developer");

public sealed record TestEscalationInput(
    ImplementationResult Implementation,
    TestReport Tests,
    int Cycle,
    string Stage = "Tester");

public sealed record DeveloperTesterFixRequest(
    ArchitectureDecision Architecture,
    ImplementationResult PreviousImplementation,
    TestReport TesterReport,
    int Cycle,
    string Stage = "ManagerDeveloperFix");

public sealed record SecurityReviewInput(
    ArchitectureDecision Architecture,
    TestReport Tests,
    int Cycle,
    string Stage = "Security");

public sealed record SecurityEscalationInput(
    ArchitectureDecision Architecture,
    TestReport Tests,
    SecurityReview Security,
    int Cycle,
    string Stage = "Security");

public sealed record ArchitectureRevisionRequest(
    ArchitectureDecision CurrentArchitecture,
    SecurityReview Security,
    int Cycle,
    string Stage = "SecurityArchitectureChallenge");

public sealed record ArchitectureCorrectionRequest(
    ArchitectureDecision InvalidArchitecture,
    string Reason,
    int Cycle,
    string Stage = "ArchitectureGovernanceCorrection");

public sealed record ImplementationCorrectionRequest(
    ArchitectureDecision Architecture,
    ImplementationResult InvalidImplementation,
    string Reason,
    int Cycle,
    string Stage = "ImplementationGovernanceCorrection");

using Microsoft.Agents.AI;

namespace AgentClient.Agents;

public sealed record AgentSet(
    AIAgent Manager,
    AIAgent PolicyManager,
    AIAgent Architect,
    AIAgent Developer,
    AIAgent Tester,
    AIAgent SecurityReviewer,
    AIAgent Reviewer,
    AIAgent FinalManager);

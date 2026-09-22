using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentClient.Agents;

internal sealed record AgentSet(
    AIAgent Manager,
    AIAgent PolicyManager,
    AIAgent Architect,
    AIAgent Developer,
    AIAgent DeveloperResultFormatter,
    AIAgent Tester,
    AIAgent SecurityReviewer,
    AIAgent Reviewer,
    AIAgent FinalManager,
    IReadOnlyList<AITool>? Tools = null,
    AIAgent? DeveloperExplorer = null,
    AIAgent? DeveloperImplementer = null,
    AIAgent? DeveloperVerifier = null);

namespace McpServer.Models;

internal sealed record WorkspaceSnapshot(string Diff, IReadOnlyList<string> ChangedFiles);

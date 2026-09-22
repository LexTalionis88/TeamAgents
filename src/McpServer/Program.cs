using McpServer.Abstractions;
using McpServer.Services;
using McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services
    .AddSingleton<IWorkspacePathResolver, WorkspacePathResolver>()
    .AddSingleton<IProcessRunner, ProcessRunner>()
    .AddSingleton<IGitWorkspaceService, GitWorkspaceService>()
    .AddSingleton<IWorkspaceSnapshotService, WorkspaceSnapshotService>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WorkspaceStatusTools>()
    .WithTools<WorkspaceFileTools>()
    .WithTools<WorkspacePatchTools>()
    .WithTools<DotnetCheckTools>()
    .WithTools<WorkspaceEvidenceTools>();

await builder.Build().RunAsync();

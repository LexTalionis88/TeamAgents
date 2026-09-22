using McpServer.Abstractions;
using McpServer.Models;
using McpServer.Services;
using McpServer.Tools;

namespace Workspace.Tests.Unit;

[TestFixture]
public sealed class McpServerTests
{
    [Test]
    public void WorkspacePathResolverRejectsPathsOutsideWorkspace()
    {
        var resolver = new WorkspacePathResolver(TestContext.CurrentContext.TestDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(
                () => resolver.ResolvePath(Path.GetFullPath("outside.txt"), mustExist: false),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => resolver.ResolvePath(Path.Combine("..", "outside.txt"), mustExist: false),
                Throws.TypeOf<InvalidOperationException>());
        });
    }

    [TestCase(".git/config")]
    [TestCase("bin/app.dll")]
    [TestCase("obj/project.assets.json")]
    public void WorkspacePathResolverRejectsBuildAndGitDirectories(string path)
    {
        var resolver = new WorkspacePathResolver(TestContext.CurrentContext.TestDirectory);

        Assert.That(resolver.IsAllowedPath(path), Is.False);
    }

    [Test]
    public async Task DotnetCheckAllowsOnlyWorkspaceCommands()
    {
        var runner = new RecordingProcessRunner();
        var tools = new DotnetCheckTools(runner);

        var result = await tools.RunDotnetCheck("dotnet test workspace.slnx --no-restore");

        Assert.That(result, Is.EqualTo("проверка выполнена"));
        Assert.That(runner.LastFileName, Is.EqualTo("dotnet"));
        Assert.That(runner.LastArguments, Is.EqualTo(new[] { "test", "workspace.slnx", "--no-restore" }));

        Assert.That(
            () => tools.RunDotnetCheck("dotnet test; Remove-Item workspace.slnx"),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public async Task WorkspaceFileToolsWriteAndReadThroughWorkspaceBoundary()
    {
        var workspace = CreateTemporaryWorkspace();
        try
        {
            var tools = new WorkspaceFileTools(new WorkspacePathResolver(workspace));

            var result = await tools.ReplaceWorkspaceFile("nested/message.txt", "привет workspace");

            Assert.That(result, Is.EqualTo("FILE_REPLACED"));
            Assert.That(tools.ReadWorkspaceFile("nested/message.txt"), Is.EqualTo("привет workspace"));
            Assert.ThrowsAsync<InvalidOperationException>(
                () => tools.ReplaceWorkspaceFile("bin/message.txt", "запрещено"));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Test]
    public void GitWorkspaceServiceRejectsUnsafePatchBeforeRunningGit()
    {
        var runner = new RecordingProcessRunner();
        var service = new GitWorkspaceService(
            new WorkspacePathResolver(TestContext.CurrentContext.TestDirectory),
            runner);

        Assert.ThrowsAsync<ArgumentException>(
            () => service.ApplyPatchAsync("diff --git a/.git/config b/.git/config", CancellationToken.None));
        Assert.That(runner.CallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GitWorkspaceServiceAcceptsBeginEndPatchForNewFile()
    {
        var workspace = CreateTemporaryWorkspace();
        try
        {
            var runner = new RecordingProcessRunner();
            var service = new GitWorkspaceService(
                new WorkspacePathResolver(workspace),
                runner);

            var result = await service.ApplyPatchAsync(
                "*** Begin Patch\n" +
                "*** Add File: src/Feature/Feature.cs\n" +
                "+namespace Feature;\n" +
                "+\n" +
                "+public sealed class FeatureMarker;\n" +
                "*** End Patch",
                CancellationToken.None);

            Assert.That(result, Is.EqualTo("PATCH_APPLIED"));
            Assert.That(
                File.ReadAllText(Path.Combine(workspace, "src", "Feature", "Feature.cs")),
                Is.EqualTo("namespace Feature;\n\npublic sealed class FeatureMarker;"));
            Assert.That(runner.CallCount, Is.EqualTo(0));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static string CreateTemporaryWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mcp-server-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public int CallCount { get; private set; }

        public string? LastFileName { get; private set; }

        public IReadOnlyList<string>? LastArguments { get; private set; }

        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? standardInput,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastFileName = fileName;
            LastArguments = arguments.ToArray();
            return Task.FromResult(new ProcessResult(0, "проверка выполнена"));
        }

        public Task<string> RunTextAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastFileName = fileName;
            LastArguments = arguments.ToArray();
            return Task.FromResult("проверка выполнена");
        }
    }
}

using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace WpfReorganize.Builder;

[Command("test-package")]
internal sealed class TestPackageCommand : ICommandHandler
{
    [Option("package")]
    public string? Package { get; init; }

    public Task<int> RunAsync()
    {
        var context = BuilderContext.Create();
        Log.Info("=== DotNetCampus.WpfLib Builder — Package Test Mode ===");
        Log.Info($"Repo root: {context.RepoRoot}");
        return Task.FromResult(PackageTestService.Run(context, Package));
    }
}

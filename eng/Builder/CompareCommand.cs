using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace WpfReorganize.Builder;

[Command("compare")]
internal sealed class CompareCommand : ICommandHandler
{
    public Task<int> RunAsync()
    {
        var context = BuilderContext.Create();
        Log.Info("=== DotNetCampus.WpfLib Builder — Compare Mode ===");
        Log.Info($"Repo root: {context.RepoRoot}");
        return Task.FromResult(CompareService.Run(context));
    }
}

using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace WpfReorganize.Builder;

[Command("clean")]
internal sealed class CleanCommand : ICommandHandler
{
    public Task<int> RunAsync()
    {
        var context = BuilderContext.Create();
        Log.Info("=== DotNetCampus.WpfLib Builder — Clean Mode ===");
        Log.Info($"Repo root: {context.RepoRoot}");
        CleanService.Run(context);
        return Task.FromResult(0);
    }
}

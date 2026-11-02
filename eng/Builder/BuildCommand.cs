using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace WpfReorganize.Builder;

[Command]
internal sealed class BuildCommand : ICommandHandler
{
    [Option("version")]
    public string? Version { get; init; }

    public Task<int> RunAsync()
    {
        var context = BuilderContext.Create();
        return Task.FromResult(BuildService.Run(context, ResolveVersion(context.RepoRoot)));
    }

    internal string ResolveVersion(string repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(Version))
            return Version;

        var prefix = WpfRuntimeDefinition.ReadCiNuGetVersionPrefix(repoRoot);
        var suffixIndex = prefix.IndexOf('-');
        return $"{(suffixIndex < 0 ? prefix : prefix[..suffixIndex])}-local";
    }
}

using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace WpfReorganize.Builder;

[Command]
internal sealed class BuildCommand : ICommandHandler
{
    [Option("version")]
    public string Version { get; init; } = "1.0.0";

    public Task<int> RunAsync() => Task.FromResult(BuildService.Run(BuilderContext.Create(), Version));
}

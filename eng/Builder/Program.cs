using DotNetCampus.Cli;
using DotNetCampus.Cli.Exceptions;
using WpfReorganize.Builder;

try
{
    return await CommandLine.Parse(args)
        .AddHandler<BuildCommand>()
        .AddHandler<CleanCommand>()
        .AddHandler<CompareCommand>()
        .AddHandler<TestPackageCommand>()
        .AddHandler<RelayPullRequestCommand>()
        .AddHandler<GitHubActionsBuildCommand>()
        .AddHandler<GitHubArtifactCommentCommand>()
        .RunAsync();
}
catch (CommandLineParseException exception)
{
    Log.Error(exception.Message);
    return 1;
}

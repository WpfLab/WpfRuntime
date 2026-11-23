using System.Net.Http;
using System.Net.Http.Headers;
using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace WpfReorganize.Builder;

[Command("publish-packages")]
internal sealed class PublishPackagesCommand : ICommandHandler
{
    [Option("repository")]
    public string? Repository { get; init; }

    [Option("run-id")]
    public long? RunId { get; init; }

    public async Task<int> RunAsync()
    {
        var repository = GitHubRepositoryAddress.ParseFullName
        (
            GitHubActionsEnvironment.GetRequired(Repository, "PUBLISH_REPOSITORY")
        );
        var runId = GitHubActionsEnvironment.GetPositiveInt64(RunId, "PUBLISH_RUN_ID");
        var githubToken = GitHubActionsEnvironment.GetRequired(null, "GITHUB_TOKEN");
        var nugetKey = GitHubActionsEnvironment.GetRequired(null, "NUGET_API_KEY");
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WpfRuntime-Builder");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, args) =>
        {
            args.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            return await new PublishPackagesService(client, ProcessRunner.RunAsync).RunAsync
            (
                repository, runId, Path.Join(Path.GetTempPath(), $"builder-publish-{Guid.NewGuid():N}"),
                nugetKey, githubToken, cancellation.Token
            ).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
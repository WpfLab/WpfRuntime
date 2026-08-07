using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace WpfReorganize.Builder;

[Command("ci-build")]
internal sealed class GitHubActionsBuildCommand : ICommandHandler
{
    [Option("repository")]
    public required string Repository { get; init; }

    [Option("event-path")]
    public string? EventPath { get; init; }

    [Option("event-name")]
    public string? EventName { get; init; }

    [Option("trusted-sha")]
    public string? TrustedSha { get; init; }

    [Option("target")]
    public required string Target { get; init; }

    [Option("ref")]
    public string? GitRef { get; init; }

    [Option("run-id")]
    public long? RunId { get; init; }

    [Option("run-attempt")]
    public long? RunAttempt { get; init; }

    [Option("github-output")]
    public string? GitHubOutput { get; init; }

    public async Task<int> RunAsync()
    {
        try
        {
            var repositoryPath = Path.GetFullPath(Repository);
            var eventPath = GitHubActionsEnvironment.GetRequired(EventPath, "GITHUB_EVENT_PATH");
            var eventName = GitHubActionsEnvironment.GetRequired(EventName, "GITHUB_EVENT_NAME");
            var trustedSha = GitHubActionsEnvironment.GetRequired(TrustedSha, "GITHUB_SHA");
            var gitRef = GitHubActionsEnvironment.GetRequired(GitRef, "GITHUB_REF");
            var runId = GitHubActionsEnvironment.GetPositiveInt64(RunId, "GITHUB_RUN_ID");
            var runAttempt = GitHubActionsEnvironment.GetPositiveInt64(RunAttempt, "GITHUB_RUN_ATTEMPT");
            var githubOutput = string.IsNullOrWhiteSpace(GitHubOutput)
                ? Environment.GetEnvironmentVariable("GITHUB_OUTPUT")
                : GitHubOutput;
            var metadata = GitHubActionsBuildMetadata.Read(eventPath, eventName, trustedSha);
            var target = ParseTarget(Target);
            using var cancellationTokenSource = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationTokenSource.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;
            try
            {
                var gitPath = await GitService.FindGitAsync(repositoryPath, cancellationTokenSource.Token)
                    .ConfigureAwait(false);
                var dotnetPath = await LocalBuildValidationService.FindDotNetAsync(
                        repositoryPath,
                        cancellationTokenSource.Token)
                    .ConfigureAwait(false);
                var msBuildPath = MsBuildService.FindMsBuild();
                if (!Path.IsPathFullyQualified(msBuildPath) || !File.Exists(msBuildPath))
                {
                    throw new FileNotFoundException(BuilderResources.MsBuildNotFound, msBuildPath);
                }
                var service = new GitHubActionsBuildService(gitPath, dotnetPath, msBuildPath);
                var identity = await service.RunAsync(
                        new GitHubActionsBuildOptions(
                            repositoryPath,
                            metadata,
                            target,
                            gitRef,
                            DateTimeOffset.UtcNow,
                            runId,
                            runAttempt),
                        cancellationTokenSource.Token)
                    .ConfigureAwait(false);

                if (target == GitHubActionsBuildTarget.Package)
                {
                    if (string.IsNullOrWhiteSpace(githubOutput))
                    {
                        throw new ArgumentException(BuilderResources.GitHubActionsOutputRequired);
                    }

                    GitHubActionsOutput.Write(
                        githubOutput,
                        new Dictionary<string, string>
                        {
                            ["tested-sha"] = identity.TestedSha.ToString(),
                            ["version"] = identity.PackageVersion,
                            ["package-path"] = identity.PackagePath,
                            ["symbol-package-path"] = identity.SymbolPackagePath,
                            ["all-symbols-archive-path"] = identity.AllSymbolsArchivePath,
                            ["artifact-name"] = identity.ArtifactName,
                        });
                }

                return 0;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (OperationCanceledException)
        {
            Log.Error("GitHub Actions build was canceled.");
            return 130;
        }
        catch (Exception exception)
        {
            Log.Error(exception.Message);
            return 1;
        }
    }

    internal static GitHubActionsBuildTarget ParseTarget(string value) =>
        value.ToLowerInvariant() switch
        {
            "solution" => GitHubActionsBuildTarget.Solution,
            "package" => GitHubActionsBuildTarget.Package,
            _ => throw new ArgumentException(BuilderResources.InvalidGitHubActionsBuildTarget, nameof(value)),
        };

}

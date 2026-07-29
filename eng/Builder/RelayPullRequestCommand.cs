using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace WpfReorganize.Builder;

[Command("relay-pr")]
internal sealed class RelayPullRequestCommand : ICommandHandler
{
    [Option("pull-request")]
    public required string PullRequest { get; init; }

    [Option("target-remote")]
    public string TargetRemote { get; init; } = "origin";

    [Option("base")]
    public string? Base { get; init; }

    [Option("github-token")]
    public string? GitHubToken { get; init; }

    [Option("allow-untrusted-build")]
    public bool AllowUntrustedBuild { get; init; }

    [Option("keep-workspace")]
    public string KeepWorkspace { get; init; } = "on-failure";

    public async Task<int> RunAsync()
    {
        try
        {
            if (!AllowUntrustedBuild)
            {
                Log.Error(BuilderResources.UntrustedBuildConsentRequired);
                return 2;
            }

            var context = BuilderContext.Create();
            var address = PullRequestAddress.Parse(PullRequest);
            var targetRemote = ResolveTargetRemote(TargetRemote);
            var baseBranch = ResolveBaseBranch(Base, targetRemote);
            var keepWorkspace = string.IsNullOrWhiteSpace(KeepWorkspace)
                ? KeepWorkspacePolicy.OnFailure
                : ParseKeepWorkspace(KeepWorkspace);
            var token = ResolveGitHubToken(
                GitHubToken,
                Environment.GetEnvironmentVariable("GITHUB_TOKEN"));
            if (token is null)
            {
                Log.Error(BuilderResources.GitHubTokenRequired);
                return 2;
            }

            using var cancellationTokenSource = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationTokenSource.Cancel();
                Log.Warn("Cancellation requested; stopping the active process tree.");
            };
            Console.CancelKeyPress += cancelHandler;
            try
            {
                var gitPath = await GitService.FindGitAsync(
                    context.RepoRoot,
                    cancellationTokenSource.Token).ConfigureAwait(false);
                var dotnetPath = await LocalBuildValidationService.FindDotNetAsync(
                    context.RepoRoot,
                    cancellationTokenSource.Token).ConfigureAwait(false);
                var msBuildPath = MsBuildService.FindMsBuild();
                if (!Path.IsPathFullyQualified(msBuildPath) || !File.Exists(msBuildPath))
                {
                    Log.Error("Visual Studio MSBuild.exe could not be resolved to an absolute existing path.");
                    return 2;
                }

                var git = new GitService(gitPath);
                var github = new GitHubPullRequestService(token);
                var validation = new LocalBuildValidationService(git, dotnetPath, msBuildPath);
                var service = new PullRequestRelayService(git, github, validation);
                Log.Info($"Source PR: {address.CanonicalUrl}");
                Log.Info($"Target remote: {targetRemote}");
                Log.Info($"Target base: {baseBranch}");
                var result = await service.RunAsync(
                    new PullRequestRelayOptions(
                        address,
                        targetRemote,
                        baseBranch,
                        AllowUntrustedBuild,
                        keepWorkspace),
                    context.RepoRoot,
                    cancellationTokenSource.Token).ConfigureAwait(false);
                if (result.PullRequestUrl is null)
                {
                    Log.Info(BuilderResources.NoChangesToRelay);
                }
                else
                {
                    Log.Info($"Target pull request: {result.PullRequestUrl}");
                }

                if (result.ValidatedCommit is not null)
                {
                    Log.Info($"Validated commit: {result.ValidatedCommit}");
                }
                if (result.WorkspaceRetained)
                {
                    Log.Info($"Relay workspace retained: {result.WorkspacePath}");
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
            Log.Error("PR relay was canceled.");
            return 130;
        }
        catch (Exception exception)
        {
            Log.Error(exception.Message);
            return 1;
        }
    }

    internal static string ResolveTargetRemote(string? targetRemote) =>
        string.IsNullOrWhiteSpace(targetRemote) ? "origin" : targetRemote;

    internal static string ResolveBaseBranch(string? baseBranch, string targetRemote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRemote);
        return string.IsNullOrWhiteSpace(baseBranch) ? $"{targetRemote}/main" : baseBranch;
    }

    internal static string? ResolveGitHubToken(string? commandLineToken, string? environmentToken) =>
        !string.IsNullOrWhiteSpace(commandLineToken)
            ? commandLineToken
            : !string.IsNullOrWhiteSpace(environmentToken)
                ? environmentToken
                : null;

    internal static KeepWorkspacePolicy ParseKeepWorkspace(string value) =>
        value.ToLowerInvariant() switch
        {
            "always" => KeepWorkspacePolicy.Always,
            "on-failure" => KeepWorkspacePolicy.OnFailure,
            "never" => KeepWorkspacePolicy.Never,
            _ => throw new ArgumentException(BuilderResources.InvalidKeepWorkspacePolicy, nameof(value)),
        };
}

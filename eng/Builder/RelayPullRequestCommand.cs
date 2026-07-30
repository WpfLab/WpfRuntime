using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace WpfReorganize.Builder;

[Command("relay-pr")]
internal sealed class RelayPullRequestCommand : ICommandHandler
{
    [Option("pull-request")]
    public string? PullRequest { get; init; }

    [Option("resume-workspace")]
    public string? ResumeWorkspace { get; init; }

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

    [Option("conflict-mode")]
    public string ConflictMode { get; init; } = "manual";

    public async Task<int> RunAsync()
    {
        try
        {
            var context = BuilderContext.Create();
            var targetRemote = ResolveTargetRemote(TargetRemote);
            var baseBranch = ResolveBaseBranch(Base, targetRemote);
            var keepWorkspace = string.IsNullOrWhiteSpace(KeepWorkspace)
                ? KeepWorkspacePolicy.OnFailure
                : ParseKeepWorkspace(KeepWorkspace);
            var conflictMode = string.IsNullOrWhiteSpace(ConflictMode)
                ? WpfReorganize.Builder.ConflictMode.Manual
                : ParseConflictMode(ConflictMode);
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
                var gitPath = await GitService.FindGitAsync
                (
                    context.RepoRoot,
                    cancellationTokenSource.Token
                ).ConfigureAwait(false);
                var git = new GitService(gitPath);
                var github = new GitHubPullRequestService(token);
                LocalBuildValidationService? validation = null;
                if (AllowUntrustedBuild)
                {
                    var dotnetPath = await LocalBuildValidationService.FindDotNetAsync
                    (
                        context.RepoRoot,
                        cancellationTokenSource.Token
                    ).ConfigureAwait(false);
                    var msBuildPath = MsBuildService.FindMsBuild();
                    if (!Path.IsPathFullyQualified(msBuildPath) || !File.Exists(msBuildPath))
                    {
                        Log.Error("Visual Studio MSBuild.exe could not be resolved to an absolute existing path.");
                        return 2;
                    }

                    validation = new LocalBuildValidationService(git, dotnetPath, msBuildPath);
                }
                else
                {
                    Log.Info("Local build validation is skipped. GitHub Actions will validate the published pull request.");
                }

                var service = new PullRequestRelayService(git, github, validation);
                PullRequestRelayResult result;
                if (!string.IsNullOrWhiteSpace(ResumeWorkspace))
                {
                    Log.Info($"Resuming relay workspace: {Path.GetFullPath(ResumeWorkspace)}");
                    result = await service.ContinueAsync
                    (
                        ResumeWorkspace,
                        context.RepoRoot,
                        keepWorkspace,
                        AllowUntrustedBuild,
                        cancellationTokenSource.Token
                    ).ConfigureAwait(false);
                }
                else
                {
                    var address = PullRequestAddress.Parse(PullRequest!);
                    Log.Info($"Source PR: {address.CanonicalUrl}");
                    Log.Info($"Target remote: {targetRemote}");
                    Log.Info($"Target base: {baseBranch}");
                    result = await service.RunAsync(
                        new PullRequestRelayOptions(
                            address,
                            targetRemote,
                            baseBranch,
                            AllowUntrustedBuild,
                            keepWorkspace,
                            conflictMode),
                        context.RepoRoot,
                        cancellationTokenSource.Token).ConfigureAwait(false);
                }
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
        catch (PatchConflictException exception)
        {
            Log.Error("The source patch conflicts with the target base and requires manual resolution.");
            Log.Error(exception.Message);
            Log.Info(CreateManualConflictInstructions(exception.PatchPath));
            return 3;
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

    internal static ConflictMode ParseConflictMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "manual" => WpfReorganize.Builder.ConflictMode.Manual,
            "fail" => WpfReorganize.Builder.ConflictMode.Fail,
            _ => throw new ArgumentException("--conflict-mode must be manual or fail.", nameof(value)),
        };

    internal static string CreateManualConflictInstructions(string patchPath)
    {
        var workspacePath = Path.GetDirectoryName(patchPath)!;
        var repositoryPath = Path.Join(workspacePath, "repository");
        return $"""
            Manual conflict resolution steps:
            1. Enter the relay repository:
               cd /d "{repositoryPath}"
            2. Apply the generated patch and keep conflicts for manual resolution:
               git apply --index --3way --ignore-space-change --ignore-whitespace "{patchPath}"
            3. Edit every conflicted file and remove the conflict markers.
            4. Stage each resolved file with git add. Do not run git commit. For example:
               git add "src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Input/Stylus/Wisp/WispLogic.cs"
            5. Chinese and English AI LLM prompts containing the full context and instructions were created here:
               "{Path.Join(workspacePath, AiPatchConflictPromptWriter.ChineseFileName)}"
               "{Path.Join(workspacePath, AiPatchConflictPromptWriter.EnglishFileName)}"
            6. Resume the relay pipeline with this directly executable command:
               dotnet run --project "{Path.Join(BuilderContext.Create().RepoRoot, "eng", "Builder", "Builder.csproj")}" -- relay-pr --resume-workspace "{workspacePath}" --allow-untrusted-build
            """;
    }
}

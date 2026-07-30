namespace WpfReorganize.Builder;

internal sealed class GitService
{
    private const string BaseReference = "refs/builder/target-base";
    private const string SourceBaseReference = "refs/builder/source-base";
    private const string SourceReference = "refs/builder/source-head";
    private readonly string _gitPath;
    private readonly TimeSpan _timeout;

    public GitService
    (
        string gitPath,
        TimeSpan? timeout = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gitPath);
        _gitPath = gitPath;
        _timeout = timeout ?? TimeSpan.FromMinutes(10);
    }

    public static async Task<string> FindGitAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        var result = await ProcessRunner.RunAsync(
            new ProcessRunOptions("where.exe", workingDirectory, "git.exe")
            {
                Timeout = TimeSpan.FromSeconds(30),
            },
            cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("git.exe was not found on PATH.");
        }

        var path = SplitLines(result.StandardOutput).FirstOrDefault(File.Exists);
        return path ?? throw new InvalidOperationException("git.exe was not found on PATH.");
    }

    public async Task<TargetRepository> ResolveTargetAsync(
        string callerRepository,
        string remoteName,
        string? requestedBaseBranch,
        int pullRequestNumber,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerRepository);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        if (remoteName.StartsWith("-", StringComparison.Ordinal)
            || remoteName.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ArgumentException($"Invalid Git remote name: {remoteName}", nameof(remoteName));
        }

        var baseBranch = ResolveBaseBranch(remoteName, requestedBaseBranch);
        await ValidateBranchNameAsync(callerRepository, baseBranch, cancellationToken).ConfigureAwait(false);
        var fetchUrl = GetSingleLine(await RunAsync(
            callerRepository,
            isolatedHome: null,
            cancellationToken,
            allowFailure: false,
            "remote",
            "get-url",
            remoteName).ConfigureAwait(false));
        var pushUrl = GetSingleLine(await RunAsync(
            callerRepository,
            isolatedHome: null,
            cancellationToken,
            allowFailure: false,
            "remote",
            "get-url",
            "--push",
            remoteName).ConfigureAwait(false));
        var address = GitHubRepositoryAddress.FromMatchingRemotes(fetchUrl, pushUrl);
        var baseSha = await GetRemoteBranchShaAsync(
            callerRepository,
            isolatedHome: null,
            fetchUrl,
            baseBranch,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Target base branch was not found: {baseBranch}");
        var relayBranch = RelayMarkers.CreateBranchName(pullRequestNumber);
        var relayBranchSha = await GetRemoteBranchShaAsync(
            callerRepository,
            isolatedHome: null,
            fetchUrl,
            relayBranch,
            cancellationToken).ConfigureAwait(false);

        return new TargetRepository(
            remoteName,
            address,
            fetchUrl,
            pushUrl,
            baseBranch,
            relayBranch,
            baseSha,
            relayBranchSha);
    }

    internal static string ResolveBaseBranch(string remoteName, string? requestedBaseBranch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        var baseBranch = string.IsNullOrWhiteSpace(requestedBaseBranch) ? "main" : requestedBaseBranch;
        var remotePrefix = $"{remoteName}/";
        return baseBranch.StartsWith(remotePrefix, StringComparison.Ordinal)
            ? baseBranch[remotePrefix.Length..]
            : baseBranch;
    }

    public async Task CloneTargetAsync(
        string callerRepository,
        TargetRepository target,
        PullRequestRelayWorkspace workspace,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerRepository);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(workspace);
        await RunAsync(
            workspace.RootPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "clone",
            "--no-checkout",
            "--origin",
            "target",
            callerRepository,
            workspace.RepositoryPath).ConfigureAwait(false);
        await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "config",
            "core.longpaths",
            "true").ConfigureAwait(false);
        await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "remote",
            "set-url",
            "target",
            target.FetchUrl).ConfigureAwait(false);
        await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "fetch",
            "--no-tags",
            "target",
            $"+refs/heads/{target.BaseBranch}:{BaseReference}").ConfigureAwait(false);

        var fetchedBaseSha = await ResolveCommitAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            BaseReference,
            cancellationToken).ConfigureAwait(false);
        if (fetchedBaseSha != target.BaseSha)
        {
            throw new InvalidOperationException(
                $"Target base moved from {target.BaseSha} to {fetchedBaseSha}; restart the relay.");
        }

        var switchResult = await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: true,
            "switch",
            "--create",
            target.RelayBranch,
            BaseReference).ConfigureAwait(false);
        if (switchResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to create relay branch '{target.RelayBranch}' from target base. {switchResult.Output.Trim()}");
        }

        await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "remote",
            "set-url",
            "--push",
            "target",
            "disabled://relay-push-is-explicit").ConfigureAwait(false);

        if (target.ExistingRelayBranchSha is not null)
        {
            const string existingRelayReference = "refs/builder/existing-relay";
            await RunAsync(
                workspace.RepositoryPath,
                workspace.IsolatedHomePath,
                cancellationToken,
                allowFailure: false,
                "fetch",
                "--no-tags",
                target.FetchUrl,
                $"+refs/heads/{target.RelayBranch}:{existingRelayReference}").ConfigureAwait(false);
            var fetchedRelaySha = await ResolveCommitAsync(
                workspace.RepositoryPath,
                workspace.IsolatedHomePath,
                existingRelayReference,
                cancellationToken).ConfigureAwait(false);
            if (fetchedRelaySha != target.ExistingRelayBranchSha.Value)
            {
                throw new InvalidOperationException(
                    $"Target relay branch moved from {target.ExistingRelayBranchSha.Value} to {fetchedRelaySha}; restart the relay.");
            }
        }
    }

    public async Task FetchSourceAsync(
        PullRequestSource source,
        PullRequestRelayWorkspace workspace,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(workspace);
        var directFetchSucceeded = false;
        if (!string.IsNullOrWhiteSpace(source.HeadCloneUrl))
        {
            var directFetch = await RunAsync(
                workspace.RepositoryPath,
                workspace.IsolatedHomePath,
                cancellationToken,
                allowFailure: true,
                "fetch",
                "--no-tags",
                source.HeadCloneUrl,
                $"+refs/heads/{source.HeadReference}:{SourceReference}").ConfigureAwait(false);
            directFetchSucceeded = directFetch.ExitCode == 0
                && await IsExpectedCommitAsync(
                    workspace.RepositoryPath,
                    workspace.IsolatedHomePath,
                    SourceReference,
                    source.HeadSha,
                    cancellationToken).ConfigureAwait(false);
        }

        if (!directFetchSucceeded)
        {
            var fallback = await RunAsync(
                workspace.RepositoryPath,
                workspace.IsolatedHomePath,
                cancellationToken,
                allowFailure: true,
                "fetch",
                "--no-tags",
                source.BaseCloneUrl,
                $"+refs/pull/{source.Address.Number}/head:{SourceReference}").ConfigureAwait(false);
            if (fallback.ExitCode != 0
                || !await IsExpectedCommitAsync(
                    workspace.RepositoryPath,
                    workspace.IsolatedHomePath,
                    SourceReference,
                    source.HeadSha,
                    cancellationToken).ConfigureAwait(false))
            {
                throw new SourceHeadUnavailableException(
                    $"Unable to fetch the exact source head {source.HeadSha} from the source branch or pull request ref.");
            }
        }

        var baseFetch = await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: true,
            "fetch",
            "--no-tags",
            source.BaseCloneUrl,
            $"+{source.BaseSha}:{SourceBaseReference}").ConfigureAwait(false);
        if (baseFetch.ExitCode != 0
            || !await IsExpectedCommitAsync(
                workspace.RepositoryPath,
                workspace.IsolatedHomePath,
                SourceBaseReference,
                source.BaseSha,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                $"Unable to fetch the exact source base {source.BaseSha} from {source.BaseRepository.FullName}/{source.BaseReference}.");
        }
    }

    public Task<GitObjectId> ApplySourceChangesAsync(
        PullRequestSource source,
        TargetRepository target,
        PullRequestRelayWorkspace workspace,
        ConflictMode conflictMode,
        CancellationToken cancellationToken) =>
        ApplySourcePatchAsync(source, target, workspace, conflictMode, cancellationToken);

    public Task<GitObjectId> ContinueSourceChangesAsync(
        PullRequestSource source,
        PullRequestRelayWorkspace workspace,
        CancellationToken cancellationToken) =>
        CreateRelayCommitAsync(source, workspace, cancellationToken);

    public async Task ValidateExistingBranchSourceAsync(
        PullRequestAddress source,
        TargetRepository target,
        PullRequestRelayWorkspace workspace,
        CancellationToken cancellationToken)
    {
        if (target.ExistingRelayBranchSha is null)
        {
            return;
        }

        const string existingRelayReference = "refs/builder/existing-relay";
        var result = await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "show",
            "--no-patch",
            "--format=%B",
            existingRelayReference).ConfigureAwait(false);
        if (!RelayMarkers.MergeMessageMatches(result.StandardOutput, source))
        {
            throw new InvalidOperationException(BuilderResources.TargetBranchSourceConflict);
        }
    }

    public async Task PushValidatedCommitAsync(
        TargetRepository target,
        GitObjectId validatedCommit,
        PullRequestRelayWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var publicationRepository = Path.Join(workspace.RootPath, $"publication-{Guid.NewGuid():N}.git");
        await RunAsync(
            workspace.RootPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "init",
            "--bare",
            publicationRepository).ConfigureAwait(false);
        await RunAsync(
            publicationRepository,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "fetch",
            "--no-tags",
            "--no-write-fetch-head",
            workspace.RepositoryPath,
            $"{validatedCommit}:refs/builder/validated").ConfigureAwait(false);
        var publicationCommit = await ResolveCommitAsync(
            publicationRepository,
            workspace.IsolatedHomePath,
            "refs/builder/validated",
            cancellationToken).ConfigureAwait(false);
        if (publicationCommit != validatedCommit)
        {
            throw new InvalidOperationException(
                $"Publication clone HEAD {publicationCommit} does not match validated commit {validatedCommit}.");
        }

        var arguments = new List<string>();
        var expectedRemoteSha = target.ExistingRelayBranchSha?.ToString() ?? string.Empty;
        arguments.Add($"--force-with-lease=refs/heads/{target.RelayBranch}:{expectedRemoteSha}");

        arguments.Add("--no-verify");
        arguments.Add(target.PushUrl);
        arguments.Add($"{validatedCommit}:refs/heads/{target.RelayBranch}");
        Log.Info(BuilderResources.GitPushAuthenticationMayPrompt);
        await RunPushAsync
        (
            publicationRepository,
            arguments,
            cancellationToken
        ).ConfigureAwait(false);
    }

    public async Task<GitObjectId> ResolveCommitAsync(
        string repositoryPath,
        string? isolatedHome,
        string revision,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            repositoryPath,
            isolatedHome,
            cancellationToken,
            allowFailure: false,
            "rev-parse",
            "--verify",
            $"{revision}^{{commit}}").ConfigureAwait(false);
        return GitObjectId.Parse(GetSingleLine(result));
    }

    public async Task<GitObjectId> ResolveTreeAsync(
        string repositoryPath,
        string? isolatedHome,
        string revision,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            repositoryPath,
            isolatedHome,
            cancellationToken,
            allowFailure: false,
            "rev-parse",
            "--verify",
            $"{revision}^{{tree}}").ConfigureAwait(false);
        return GitObjectId.Parse(GetSingleLine(result));
    }

    public async Task<IReadOnlyList<string>> GetTrackedChangesAsync(
        string repositoryPath,
        string? isolatedHome,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            repositoryPath,
            isolatedHome,
            cancellationToken,
            allowFailure: false,
            "status",
            "--porcelain=v1",
            "--untracked-files=no").ConfigureAwait(false);
        return SplitLines(result.StandardOutput);
    }

    private async Task<GitObjectId?> GetRemoteBranchShaAsync(
        string workingDirectory,
        string? isolatedHome,
        string remoteUrl,
        string branch,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            workingDirectory,
            isolatedHome,
            cancellationToken,
            allowFailure: true,
            "ls-remote",
            "--exit-code",
            "--heads",
            remoteUrl,
            $"refs/heads/{branch}").ConfigureAwait(false);
        if (result.ExitCode == 2)
        {
            return null;
        }
        if (result.ExitCode != 0)
        {
            throw CreateGitException(result);
        }

        var line = SplitLines(result.StandardOutput).SingleOrDefault()
            ?? throw new InvalidOperationException($"Git returned no SHA for refs/heads/{branch}.");
        var sha = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? throw new InvalidOperationException($"Git returned an invalid ls-remote line: {line}");
        return GitObjectId.Parse(sha);
    }

    private async Task<GitObjectId> ApplySourcePatchAsync(
        PullRequestSource source,
        TargetRepository target,
        PullRequestRelayWorkspace workspace,
        ConflictMode conflictMode,
        CancellationToken cancellationToken)
    {
        var mergeBaseResult = await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: true,
            "merge-base",
            source.BaseSha.ToString(),
            source.HeadSha.ToString()).ConfigureAwait(false);
        var mergeBase = mergeBaseResult.ExitCode == 0
            ? GitObjectId.Parse(GetSingleLine(mergeBaseResult))
            : throw new InvalidOperationException(
                $"Unable to determine the source pull request diff base. {mergeBaseResult.Output.Trim()}");

        var patchPath = Path.Join(workspace.RootPath, "source.patch");
        var diffResult = await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "diff",
            "--binary",
            "--full-index",
            mergeBase.ToString(),
            source.HeadSha.ToString()).ConfigureAwait(false);
        await File.WriteAllTextAsync(patchPath, diffResult.StandardOutput, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(diffResult.StandardOutput))
        {
            var currentCommit = await ResolveCommitAsync(
                workspace.RepositoryPath,
                workspace.IsolatedHomePath,
                "HEAD",
                cancellationToken).ConfigureAwait(false);
            throw new NoChangesToRelayException(currentCommit);
        }

        var applyResult = await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: true,
            "apply",
            "--index",
            "--3way",
            "--ignore-space-change",
            "--ignore-whitespace",
            patchPath).ConfigureAwait(false);
        if (applyResult.ExitCode != 0)
        {
            await RunAsync(
                workspace.RepositoryPath,
                workspace.IsolatedHomePath,
                cancellationToken,
                allowFailure: true,
                "reset",
                "--hard",
                target.BaseSha.ToString()).ConfigureAwait(false);
            if (conflictMode == ConflictMode.Manual)
            {
                throw new PatchConflictException(patchPath, applyResult.Output.Trim());
            }

            throw new InvalidOperationException(
                $"Unable to apply source pull request changes to the target base while ignoring whitespace differences. {applyResult.Output.Trim()}");
        }

        return await CommitAppliedChangesAsync(source, workspace, cancellationToken).ConfigureAwait(false);
    }

    private async Task<GitObjectId> CommitAppliedChangesAsync(
        PullRequestSource source,
        PullRequestRelayWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var stagedChanges = await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: true,
            "diff",
            "--cached",
            "--quiet").ConfigureAwait(false);
        if (stagedChanges.ExitCode == 0)
        {
            var currentCommit = await ResolveCommitAsync(
                workspace.RepositoryPath,
                workspace.IsolatedHomePath,
                "HEAD",
                cancellationToken).ConfigureAwait(false);
            throw new NoChangesToRelayException(currentCommit);
        }
        if (stagedChanges.ExitCode != 1)
        {
            throw CreateGitException(stagedChanges);
        }

        return await CreateRelayCommitAsync(source, workspace, cancellationToken).ConfigureAwait(false);
    }

    private async Task<GitObjectId> CreateRelayCommitAsync(
        PullRequestSource source,
        PullRequestRelayWorkspace workspace,
        CancellationToken cancellationToken)
    {
        await RunAsync
        (
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "-c",
            "user.name=WpfRuntime Bot",
            "-c",
            "user.email=wpfruntime-bot@users.noreply.github.com",
            "commit",
            "--no-verify",
            $"--author={source.AuthorName} <{source.AuthorEmail}>",
            "--message",
            RelayMarkers.CreatePatchMessage(source)
        ).ConfigureAwait(false);
        return await ResolveCommitAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            "HEAD",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsExpectedCommitAsync(
        string repositoryPath,
        string? isolatedHome,
        string reference,
        GitObjectId expected,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ResolveCommitAsync(repositoryPath, isolatedHome, reference, cancellationToken)
                .ConfigureAwait(false) == expected;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async Task RunPushAsync
    (
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        var options = new ProcessRunOptions(_gitPath, workingDirectory, ["push", .. arguments])
        {
            Timeout = _timeout,
            InheritEnvironment = true,
            EnvironmentVariables = ProcessEnvironment.CreateGitPushEnvironment(),
        };
        var result = await ProcessRunner.RunAsync(options, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw CreateGitException(result, ["push", .. arguments]);
        }
    }

    private async Task<ProcessResult> RunAsync(
        string workingDirectory,
        string? isolatedHome,
        CancellationToken cancellationToken,
        bool allowFailure,
        params string[] arguments)
    {
        var options = new ProcessRunOptions(_gitPath, workingDirectory, arguments)
        {
            Timeout = _timeout,
            InheritEnvironment = true,
            EnvironmentVariables = ProcessEnvironment.CreateGitEnvironment(
                isolatedHome ?? Path.GetTempPath()),
        };
        var result = await ProcessRunner.RunAsync(options, cancellationToken).ConfigureAwait(false);
        if (!allowFailure && result.ExitCode != 0)
        {
            throw CreateGitException(result, arguments);
        }

        return result;
    }

    private async Task ValidateBranchNameAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(branchName) || branchName.StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Invalid Git branch name: {branchName}", nameof(branchName));
        }

        var result = await RunAsync(
            repositoryPath,
            isolatedHome: null,
            cancellationToken,
            allowFailure: true,
            "check-ref-format",
            "--branch",
            branchName).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new ArgumentException($"Invalid Git branch name: {branchName}", nameof(branchName));
        }
    }

    private static InvalidOperationException CreateGitException(ProcessResult result, IReadOnlyList<string>? arguments = null)
    {
        var operation = arguments is null || arguments.Count == 0
            ? "Git command"
            : $"git {string.Join(' ', arguments.Take(3))}";
        var detail = result.Output.Trim();
        return new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
            ? $"{operation} failed with exit code {result.ExitCode}."
            : $"{operation} failed with exit code {result.ExitCode}. {detail}");
    }

    private static string GetSingleLine(ProcessResult result) =>
        SplitLines(result.StandardOutput).Single();

    private static string[] SplitLines(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

}

internal sealed class SourceHeadUnavailableException : InvalidOperationException
{
    public SourceHeadUnavailableException(string message)
        : base(message)
    {
    }
}

internal sealed class NoChangesToRelayException : InvalidOperationException
{
    public NoChangesToRelayException(GitObjectId relayCommit)
        : base(BuilderResources.NoChangesToRelay)
    {
        RelayCommit = relayCommit;
    }

    public GitObjectId RelayCommit { get; }
}

internal sealed class PatchConflictException : InvalidOperationException
{
    public PatchConflictException(string patchPath, string gitOutput)
        : base(gitOutput)
    {
        PatchPath = patchPath;
    }

    public string PatchPath { get; }
}

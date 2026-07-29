namespace WpfReorganize.Builder;

internal sealed class GitService
{
    private const string BaseReference = "refs/builder/target-base";
    private const string SourceReference = "refs/builder/source-head";
    private readonly string _gitPath;
    private readonly TimeSpan _timeout;

    public GitService(string gitPath, TimeSpan? timeout = null)
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

    public async Task<string?> GetCurrentBranchAsync(
        string callerRepository,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            callerRepository,
            isolatedHome: null,
            cancellationToken,
            allowFailure: true,
            "symbolic-ref",
            "--quiet",
            "--short",
            "HEAD").ConfigureAwait(false);
        return result.ExitCode == 0 ? GetSingleLine(result) : null;
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

        var baseBranch = requestedBaseBranch;
        if (string.IsNullOrWhiteSpace(baseBranch))
        {
            baseBranch = await GetCurrentBranchAsync(callerRepository, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(BuilderResources.DetachedHeadRequiresBase);
        }

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

    public async Task CloneTargetAsync(
        TargetRepository target,
        PullRequestRelayWorkspace workspace,
        CancellationToken cancellationToken)
    {
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
            target.FetchUrl,
            workspace.RepositoryPath).ConfigureAwait(false);
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

        await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "switch",
            "--create",
            target.RelayBranch,
            BaseReference).ConfigureAwait(false);
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
    }

    public async Task<GitObjectId> MergeSourceAsync(
        PullRequestSource source,
        TargetRepository target,
        PullRequestRelayWorkspace workspace,
        CancellationToken cancellationToken)
    {
        await RequireAncestorAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            source.BaseSha,
            target.BaseSha,
            "The source PR base is not an ancestor of the target base.",
            cancellationToken).ConfigureAwait(false);
        await RequireCommonHistoryAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            target.BaseSha,
            source.HeadSha,
            cancellationToken).ConfigureAwait(false);
        await ValidateSourceCommitRangeAsync(source, target, workspace, cancellationToken).ConfigureAwait(false);

        var mergeResult = await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: true,
            "-c",
            "user.name=WpfReorganize Builder",
            "-c",
            "user.email=builder@users.noreply.github.com",
            "merge",
            "--no-ff",
            "--no-edit",
            "--message",
            RelayMarkers.CreateMergeMessage(source),
            SourceReference).ConfigureAwait(false);
        if (mergeResult.ExitCode != 0)
        {
            var conflicts = await GetConflictFilesAsync(workspace, cancellationToken).ConfigureAwait(false);
            await RunAsync(
                workspace.RepositoryPath,
                workspace.IsolatedHomePath,
                cancellationToken,
                allowFailure: true,
                "merge",
                "--abort").ConfigureAwait(false);
            var suffix = conflicts.Count == 0 ? string.Empty : $" Conflicts: {string.Join(", ", conflicts)}";
            throw new InvalidOperationException($"Git merge failed.{suffix}\n{mergeResult.Output.Trim()}");
        }

        var mergedCommit = await ResolveCommitAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            "HEAD",
            cancellationToken).ConfigureAwait(false);
        var hasChanges = await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: true,
            "diff",
            "--quiet",
            target.BaseSha.ToString(),
            mergedCommit.ToString()).ConfigureAwait(false);
        if (hasChanges.ExitCode == 0)
        {
            throw new NoChangesToRelayException(mergedCommit);
        }
        if (hasChanges.ExitCode != 1)
        {
            throw CreateGitException(hasChanges);
        }

        return mergedCommit;
    }

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
        await RunAsync(
            publicationRepository,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            ["push", .. arguments]).ConfigureAwait(false);
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

    private async Task ValidateSourceCommitRangeAsync(
        PullRequestSource source,
        TargetRepository target,
        PullRequestRelayWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: false,
            "rev-list",
            "--reverse",
            $"{target.BaseSha}..{source.HeadSha}").ConfigureAwait(false);
        var introducedCommits = SplitLines(result.StandardOutput)
            .Select(GitObjectId.Parse)
            .ToHashSet();
        var unexpectedCommits = introducedCommits.Except(source.CommitShas).ToArray();
        if (unexpectedCommits.Length > 0)
        {
            throw new InvalidOperationException(
                $"The relay would introduce commits outside the source pull request: {string.Join(", ", unexpectedCommits)}");
        }
    }

    private async Task RequireAncestorAsync(
        string repositoryPath,
        string? isolatedHome,
        GitObjectId ancestor,
        GitObjectId descendant,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            repositoryPath,
            isolatedHome,
            cancellationToken,
            allowFailure: true,
            "merge-base",
            "--is-ancestor",
            ancestor.ToString(),
            descendant.ToString()).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            throw new InvalidOperationException(errorMessage);
        }
        if (result.ExitCode != 0)
        {
            throw CreateGitException(result);
        }
    }

    private async Task RequireCommonHistoryAsync(
        string repositoryPath,
        string? isolatedHome,
        GitObjectId left,
        GitObjectId right,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            repositoryPath,
            isolatedHome,
            cancellationToken,
            allowFailure: true,
            "merge-base",
            left.ToString(),
            right.ToString()).ConfigureAwait(false);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            throw new InvalidOperationException("The source head and target base do not share Git history.");
        }
    }

    private async Task<IReadOnlyList<string>> GetConflictFilesAsync(
        PullRequestRelayWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            workspace.RepositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken,
            allowFailure: true,
            "diff",
            "--name-only",
            "--diff-filter=U").ConfigureAwait(false);
        return result.ExitCode == 0 ? SplitLines(result.StandardOutput) : [];
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
            throw CreateGitException(result);
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

    private static InvalidOperationException CreateGitException(ProcessResult result) =>
        new($"Git command failed with exit code {result.ExitCode}. Credential helper output is intentionally omitted.");

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
    public NoChangesToRelayException(GitObjectId mergedCommit)
        : base(BuilderResources.NoChangesToRelay)
    {
        MergedCommit = mergedCommit;
    }

    public GitObjectId MergedCommit { get; }
}

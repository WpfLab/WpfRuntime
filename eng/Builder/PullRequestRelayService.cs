namespace WpfReorganize.Builder;

internal sealed class PullRequestRelayService
{
    private readonly GitService _git;
    private readonly GitHubPullRequestService _github;
    private readonly LocalBuildValidationService _localValidation;

    public PullRequestRelayService(
        GitService git,
        GitHubPullRequestService github,
        LocalBuildValidationService localValidation)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(github);
        ArgumentNullException.ThrowIfNull(localValidation);
        _git = git;
        _github = github;
        _localValidation = localValidation;
    }

    public async Task<PullRequestRelayResult> RunAsync(
        PullRequestRelayOptions options,
        string callerRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerRepository);
        if (!options.AllowUntrustedBuild)
        {
            throw new InvalidOperationException(BuilderResources.UntrustedBuildConsentRequired);
        }

        PullRequestRelayWorkspace? workspace = null;
        var succeeded = false;
        var canceled = false;
        var state = new PullRequestRelayState
        {
            Stage = PullRequestRelayStage.InputValidated,
            SourcePullRequestUrl = options.PullRequest.CanonicalUrl,
        };
        try
        {
            var target = await _git.ResolveTargetAsync(
                callerRepository,
                options.TargetRemote,
                options.BaseBranch,
                options.PullRequest.Number,
                cancellationToken).ConfigureAwait(false);
            state.Stage = PullRequestRelayStage.TargetResolved;
            state.TargetRepository = target.Address.FullName;
            state.TargetRemote = target.RemoteName;
            state.TargetPushUrl = target.PushUrl;
            state.TargetBaseBranch = target.BaseBranch;
            state.TargetRelayBranch = target.RelayBranch;
            state.ExistingRemoteBranchSha = target.ExistingRelayBranchSha?.ToString();

            var source = await _github.GetSourceAsync(options.PullRequest, cancellationToken).ConfigureAwait(false);
            ValidateSource(source, options.PullRequest);
            state.Stage = PullRequestRelayStage.SourceResolved;
            state.SourcePullRequestUrl = source.Address.CanonicalUrl;
            state.SourceHeadSha = source.HeadSha.ToString();
            var existingPullRequest = target.ExistingRelayBranchSha is null
                ? null
                : await _github.FindMatchingOpenTargetPullRequestAsync(
                    target,
                    source.Address,
                    cancellationToken).ConfigureAwait(false);

            workspace = PullRequestRelayWorkspace.Create(source.Address);
            await workspace.WriteStateAsync(state, cancellationToken).ConfigureAwait(false);
            Log.Info($"Relay workspace: {workspace.RootPath}");
            await _git.CloneTargetAsync(callerRepository, target, workspace, cancellationToken).ConfigureAwait(false);
            state.Stage = PullRequestRelayStage.RepositoryCloned;
            await workspace.WriteStateAsync(state, cancellationToken).ConfigureAwait(false);
            if (existingPullRequest is null)
            {
                await _git.ValidateExistingBranchSourceAsync(
                    source.Address,
                    target,
                    workspace,
                    cancellationToken).ConfigureAwait(false);
            }

            source = await FetchSourceWithSingleRefreshAsync(
                source,
                workspace,
                state,
                cancellationToken).ConfigureAwait(false);
            state.Stage = PullRequestRelayStage.SourceFetched;
            await workspace.WriteStateAsync(state, cancellationToken).ConfigureAwait(false);

            GitObjectId relayCommit;
            try
            {
                relayCommit = await _git.ApplySourceChangesAsync(
                    source,
                    target,
                    workspace,
                    options.ConflictMode,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (NoChangesToRelayException exception)
            {
                state.Stage = PullRequestRelayStage.ChangesApplied;
                state.RelayCommitSha = exception.RelayCommit.ToString();
                await workspace.WriteStateAsync(state, cancellationToken).ConfigureAwait(false);
                Log.Info(BuilderResources.NoChangesToRelay);
                succeeded = true;
                return CreateResult(null, workspace, null, options.KeepWorkspace, succeeded);
            }
            catch (PatchConflictException exception)
            {
                state.Stage = PullRequestRelayStage.ConflictResolutionRequired;
                await workspace.WriteStateAsync(state, CancellationToken.None).ConfigureAwait(false);
                await AiPatchConflictPromptWriter.WriteAsync
                (
                    new AiPatchConflictPromptContext
                    (
                        workspace.RootPath,
                        source.Address.CanonicalUrl,
                        source.BaseSha.ToString(),
                        source.HeadSha.ToString(),
                        target.Address.FullName,
                        target.BaseBranch,
                        workspace.RepositoryPath,
                        exception.PatchPath,
                        workspace.RootPath,
                        Path.Join(callerRepository, "eng", "Builder", "Builder.csproj")
                    ),
                    CancellationToken.None
                ).ConfigureAwait(false);
                throw;
            }

            state.Stage = PullRequestRelayStage.ChangesApplied;
            state.RelayCommitSha = relayCommit.ToString();
            await workspace.WriteStateAsync(state, cancellationToken).ConfigureAwait(false);
            var validation = await _localValidation.ValidateAsync(
                source,
                target,
                relayCommit,
                workspace,
                state,
                cancellationToken).ConfigureAwait(false);
            state.Stage = PullRequestRelayStage.LocalValidationSucceeded;
            await workspace.WriteStateAsync(state, cancellationToken).ConfigureAwait(false);

            if (existingPullRequest is not null)
            {
                Log.Info($"Existing target pull request (not yet updated): {existingPullRequest}");
            }

            await _git.PushValidatedCommitAsync(
                target,
                validation.CommitSha,
                workspace,
                cancellationToken).ConfigureAwait(false);
            state.Stage = PullRequestRelayStage.BranchPushed;
            await workspace.WriteStateAsync(state, CancellationToken.None).ConfigureAwait(false);
            Log.Info($"Relay branch published: {CreateBranchUrl(target)}");
            var targetPullRequest = await _github.CreateOrReuseTargetPullRequestAsync(
                target,
                source,
                validation.CompletedAtUtc,
                cancellationToken).ConfigureAwait(false);
            state.Stage = PullRequestRelayStage.PullRequestCreatedOrReused;
            state.TargetPullRequestUrl = targetPullRequest.AbsoluteUri;
            await workspace.WriteStateAsync(state, CancellationToken.None).ConfigureAwait(false);
            succeeded = true;
            return CreateResult(
                targetPullRequest,
                workspace,
                validation.CommitSha,
                options.KeepWorkspace,
                succeeded);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
            throw;
        }
        finally
        {
            var publicationNeedsRecovery = !succeeded && state.Stage >= PullRequestRelayStage.BranchPushed;
            var manualConflictNeedsRecovery = state.Stage == PullRequestRelayStage.ConflictResolutionRequired;
            if (workspace is not null
                && !canceled
                && !publicationNeedsRecovery
                && !manualConflictNeedsRecovery
                && !ShouldKeepWorkspace(options.KeepWorkspace, succeeded))
            {
                try
                {
                    workspace.Delete();
                }
                catch (Exception exception)
                {
                    Log.Warn($"Unable to delete relay workspace '{workspace.RootPath}': {exception.Message}");
                }
            }
        }
    }

    public async Task<PullRequestRelayResult> ContinueAsync(
        string workspacePath,
        string callerRepository,
        KeepWorkspacePolicy keepWorkspace,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerRepository);
        var workspace = PullRequestRelayWorkspace.Open(workspacePath);
        var state = await workspace.ReadStateAsync(cancellationToken).ConfigureAwait(false);
        var sourceAddress = PullRequestAddress.Parse(state.SourcePullRequestUrl!);
        var source = await _github.GetSourceAsync(sourceAddress, cancellationToken).ConfigureAwait(false);
        var target = await _git.ResolveTargetAsync(
            callerRepository,
            state.TargetRemote!,
            state.TargetBaseBranch,
            source.Address.Number,
            cancellationToken).ConfigureAwait(false);
        var succeeded = false;
        try
        {
            var relayCommit = await _git.ContinueSourceChangesAsync(
                source,
                workspace,
                cancellationToken).ConfigureAwait(false);
            state.Stage = PullRequestRelayStage.ChangesApplied;
            state.RelayCommitSha = relayCommit.ToString();
            await workspace.WriteStateAsync(state, cancellationToken).ConfigureAwait(false);

            var validation = await _localValidation.ValidateAsync(
                source,
                target,
                relayCommit,
                workspace,
                state,
                cancellationToken).ConfigureAwait(false);
            state.Stage = PullRequestRelayStage.LocalValidationSucceeded;
            await workspace.WriteStateAsync(state, cancellationToken).ConfigureAwait(false);

            await _git.PushValidatedCommitAsync(
                target,
                validation.CommitSha,
                workspace,
                cancellationToken).ConfigureAwait(false);
            state.Stage = PullRequestRelayStage.BranchPushed;
            await workspace.WriteStateAsync(state, CancellationToken.None).ConfigureAwait(false);
            Log.Info($"Relay branch published: {CreateBranchUrl(target)}");

            var targetPullRequest = await _github.CreateOrReuseTargetPullRequestAsync(
                target,
                source,
                validation.CompletedAtUtc,
                cancellationToken).ConfigureAwait(false);
            state.Stage = PullRequestRelayStage.PullRequestCreatedOrReused;
            state.TargetPullRequestUrl = targetPullRequest.AbsoluteUri;
            await workspace.WriteStateAsync(state, CancellationToken.None).ConfigureAwait(false);
            succeeded = true;
            return CreateResult(targetPullRequest, workspace, validation.CommitSha, keepWorkspace, succeeded);
        }
        finally
        {
            if (succeeded && !ShouldKeepWorkspace(keepWorkspace, succeeded))
            {
                workspace.Delete();
            }
        }
    }

    private async Task<PullRequestSource> FetchSourceWithSingleRefreshAsync(
        PullRequestSource source,
        PullRequestRelayWorkspace workspace,
        PullRequestRelayState state,
        CancellationToken cancellationToken)
    {
        try
        {
            await _git.FetchSourceAsync(source, workspace, cancellationToken).ConfigureAwait(false);
            return source;
        }
        catch (SourceHeadUnavailableException)
        {
            var refreshed = await _github.RefreshSourceOnceAsync(source, cancellationToken).ConfigureAwait(false);
            ValidateSource(refreshed, source.Address);
            if (refreshed.HeadSha == source.HeadSha)
            {
                throw;
            }

            state.SourceHeadSha = refreshed.HeadSha.ToString();
            await workspace.WriteStateAsync(state, cancellationToken).ConfigureAwait(false);
            await _git.FetchSourceAsync(refreshed, workspace, cancellationToken).ConfigureAwait(false);
            return refreshed;
        }
    }

    private static void ValidateSource(PullRequestSource source, PullRequestAddress requestedAddress)
    {
        if (!string.Equals(source.State, "open", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(BuilderResources.PullRequestNotOpen);
        }

        if (!string.Equals(source.Address.SourceKey, requestedAddress.SourceKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"GitHub returned a different pull request identity: {source.Address.CanonicalUrl}");
        }
    }

    private static PullRequestRelayResult CreateResult(
        Uri? pullRequestUrl,
        PullRequestRelayWorkspace workspace,
        GitObjectId? validatedCommit,
        KeepWorkspacePolicy policy,
        bool succeeded)
    {
        var retained = ShouldKeepWorkspace(policy, succeeded);
        return new PullRequestRelayResult(pullRequestUrl, workspace.RootPath, validatedCommit, retained);
    }

    private static Uri CreateBranchUrl(TargetRepository target) =>
        new($"https://github.com/{target.Address.Owner}/{target.Address.Repository}/tree/" +
            string.Join('/', target.RelayBranch.Split('/').Select(Uri.EscapeDataString)));

    private static bool ShouldKeepWorkspace(KeepWorkspacePolicy policy, bool succeeded) =>
        policy == KeepWorkspacePolicy.Always
        || (policy == KeepWorkspacePolicy.OnFailure && !succeeded);
}

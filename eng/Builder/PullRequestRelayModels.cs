namespace WpfReorganize.Builder;

internal enum KeepWorkspacePolicy
{
    Always,
    OnFailure,
    Never,
}

internal enum PullRequestRelayStage
{
    InputValidated,
    TargetResolved,
    SourceResolved,
    RepositoryCloned,
    SourceFetched,
    Merged,
    LocalValidationSucceeded,
    BranchPushed,
    PullRequestCreatedOrReused,
}

internal sealed record PullRequestSource(
    PullRequestAddress Address,
    string Title,
    string State,
    bool IsDraft,
    GitHubRepositoryAddress? HeadRepository,
    string? HeadCloneUrl,
    string HeadReference,
    GitObjectId HeadSha,
    GitHubRepositoryAddress BaseRepository,
    string BaseCloneUrl,
    string BaseReference,
    GitObjectId BaseSha,
    IReadOnlySet<GitObjectId> CommitShas);

internal sealed record TargetRepository(
    string RemoteName,
    GitHubRepositoryAddress Address,
    string FetchUrl,
    string PushUrl,
    string BaseBranch,
    string RelayBranch,
    GitObjectId BaseSha,
    GitObjectId? ExistingRelayBranchSha);

internal sealed record PullRequestRelayOptions(
    PullRequestAddress PullRequest,
    string TargetRemote,
    string? BaseBranch,
    bool AllowUntrustedBuild,
    KeepWorkspacePolicy KeepWorkspace);

internal sealed record PullRequestRelayResult(
    Uri? PullRequestUrl,
    string WorkspacePath,
    GitObjectId? ValidatedCommit,
    bool WorkspaceRetained);

internal sealed record PullRequestRelayState
{
    public PullRequestRelayStage Stage { get; set; }

    public string? SourcePullRequestUrl { get; set; }

    public string? SourceHeadSha { get; set; }

    public string? TargetRepository { get; set; }

    public string? TargetPushUrl { get; set; }

    public string? TargetBaseBranch { get; set; }

    public string? TargetRelayBranch { get; set; }

    public string? MergedCommitSha { get; set; }

    public string? MergedTreeSha { get; set; }

    public string? ExistingRemoteBranchSha { get; set; }

    public string? TargetPullRequestUrl { get; set; }

    public List<LocalValidationGateState> ValidationGates { get; init; } = [];
}

internal sealed record LocalValidationGateState
{
    public required string Name { get; init; }

    public required string LogPath { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public int ExitCode { get; init; }
}

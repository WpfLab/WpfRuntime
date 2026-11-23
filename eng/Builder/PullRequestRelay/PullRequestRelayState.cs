namespace WpfReorganize.Builder;

internal sealed record PullRequestRelayState
{
    public PullRequestRelayStage Stage { get; set; }

    public bool PreserveWorkspace { get; init; }

    public string? SourcePullRequestUrl { get; set; }

    public string? SourceHeadSha { get; set; }

    public string? TargetRepository { get; set; }

    public string? TargetRemote { get; set; }

    public string? TargetPushUrl { get; set; }

    public string? TargetBaseBranch { get; set; }

    public string? TargetRelayBranch { get; set; }

    public string? RelayCommitSha { get; set; }

    public string? RelayTreeSha { get; set; }

    public string? ExistingRemoteBranchSha { get; set; }

    public string? TargetPullRequestUrl { get; set; }

    public List<LocalValidationGateState> ValidationGates { get; init; } = [];
}
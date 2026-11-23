namespace WpfReorganize.Builder;

internal sealed record PullRequestRelayOptions(
    PullRequestAddress PullRequest,
    string TargetRemote,
    string? BaseBranch,
    bool AllowUntrustedBuild,
    ConflictMode ConflictMode,
    string? WorkspaceRoot);
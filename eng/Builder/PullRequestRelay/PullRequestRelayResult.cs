namespace WpfReorganize.Builder;

internal sealed record PullRequestRelayResult
(
    Uri? PullRequestUrl,
    string WorkspacePath,
    GitObjectId? ValidatedCommit,
    bool WorkspaceRetained
);
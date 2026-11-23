namespace WpfReorganize.Builder;

internal sealed record PullRequestSource
(
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
    IReadOnlySet<GitObjectId> CommitShas,
    string AuthorName,
    string AuthorEmail
);
namespace WpfReorganize.Builder;

internal sealed record GitHubActionsBuildOptions
(
    string RepositoryPath,
    GitHubActionsBuildMetadata Metadata,
    GitHubActionsBuildTarget Target,
    string GitRef,
    DateTimeOffset BuildTime,
    long RunId,
    long RunAttempt
);
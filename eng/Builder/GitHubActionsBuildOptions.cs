namespace WpfReorganize.Builder;

internal enum GitHubActionsBuildTarget
{
    Solution,
    Package,
}

internal sealed record GitHubActionsBuildOptions(
    string RepositoryPath,
    GitHubActionsBuildMetadata Metadata,
    GitHubActionsBuildTarget Target,
    string GitRef,
    DateTimeOffset BuildTime,
    long RunId,
    long RunAttempt);

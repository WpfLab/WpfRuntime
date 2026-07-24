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
    long RunId,
    long RunAttempt);

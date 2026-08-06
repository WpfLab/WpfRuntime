namespace WpfReorganize.Builder;

internal sealed record GitHubActionsBuildIdentity(
    GitObjectId TestedSha,
    string PackageVersion,
    string PackagePath,
    string SymbolPackagePath,
    string ArtifactName)
{
    public static GitHubActionsBuildIdentity Create(
        GitHubActionsBuildMetadata metadata,
        string repositoryPath,
        string testedSha,
        string gitRef,
        DateTimeOffset buildTime,
        long runId,
        long runAttempt)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(gitRef);
        if (runId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runId));
        }
        if (runAttempt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runAttempt));
        }

        var parsedTestedSha = GitObjectId.Parse(testedSha);
        var packageVersion = CreatePackageVersion(gitRef, buildTime, parsedTestedSha);
        var artifactIdentity = metadata.IsPullRequest
            ? $"pr-{metadata.PullRequestNumber}"
            : $"event-{metadata.EventName}";
        var packagePath = Path.GetFullPath(Path.Join(
            repositoryPath,
            "eng",
            "Builder",
            "bin",
            "nupkg",
            $"{PackageMetadata.Id}.{packageVersion}.nupkg"));
        var symbolPackagePath = Path.ChangeExtension(packagePath, ".snupkg");
        var artifactName =
            $"{PackageMetadata.Id}-nupkg-{artifactIdentity}-sha-{parsedTestedSha}-run-{runId}-attempt-{runAttempt}-version-{packageVersion}";

        return new GitHubActionsBuildIdentity(
            parsedTestedSha,
            packageVersion,
            packagePath,
            symbolPackagePath,
            artifactName);
    }

    private static string CreatePackageVersion(
        string gitRef,
        DateTimeOffset buildTime,
        GitObjectId testedSha)
    {
        const string tagPrefix = "refs/tags/";
        if (gitRef.StartsWith(tagPrefix, StringComparison.Ordinal))
        {
            var tag = gitRef[tagPrefix.Length..];
            if (tag.StartsWith('v') && tag.Length > 1 && char.IsDigit(tag[1]))
            {
                tag = tag[1..];
            }

            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("The Git tag must contain a package version.", nameof(gitRef));
            }

            return tag;
        }

        return $"0.0.0-test.{buildTime.UtcDateTime:yyyyMMddHHmmss}.sha{testedSha.Short6}";
    }
}

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
        long runId,
        long runAttempt)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        if (runId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runId));
        }
        if (runAttempt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runAttempt));
        }

        var parsedTestedSha = GitObjectId.Parse(testedSha);
        var packageVersion = metadata.IsPullRequest
            ? $"0.0.0-pr.{metadata.PullRequestNumber}.sha{parsedTestedSha.Short}"
            : $"0.0.0-ci.sha{parsedTestedSha.Short}";
        var artifactIdentity = metadata.IsPullRequest
            ? $"pr-{metadata.PullRequestNumber}"
            : $"event-{metadata.EventName}";
        var packagePath = Path.GetFullPath(Path.Join(
            repositoryPath,
            "eng",
            "Builder",
            "bin",
            "nupkg",
            $"DotNetCampus.WpfLib.{packageVersion}.nupkg"));
        var symbolPackagePath = Path.ChangeExtension(packagePath, ".snupkg");
        var artifactName =
            $"DotNetCampus.WpfLib-nupkg-{artifactIdentity}-sha-{parsedTestedSha}-run-{runId}-attempt-{runAttempt}";

        return new GitHubActionsBuildIdentity(
            parsedTestedSha,
            packageVersion,
            packagePath,
            symbolPackagePath,
            artifactName);
    }
}

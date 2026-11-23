namespace WpfReorganize.Builder;

internal sealed record LocalBuildValidationResult(
    GitObjectId CommitSha,
    GitObjectId TreeSha,
    string PackagePath,
    DateTimeOffset CompletedAtUtc);
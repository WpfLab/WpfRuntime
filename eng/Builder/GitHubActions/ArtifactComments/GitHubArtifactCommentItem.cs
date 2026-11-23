namespace WpfReorganize.Builder;

internal sealed record GitHubArtifactCommentItem(
    long Id,
    string Name,
    int SizeInBytes,
    DateTime ExpiresAt,
    GitObjectId TestedSha,
    string PackageVersion);
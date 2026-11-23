namespace WpfReorganize.Builder;

internal sealed record GitHubArtifactCommentResult(
    bool Updated,
    int? PullRequestNumber,
    long? RunAttempt,
    string Message)
{
    public static GitHubArtifactCommentResult Skip(string reason) => new(false, null, null, reason);

    public static GitHubArtifactCommentResult Success(
        int pullRequestNumber,
        long runAttempt,
        string message) =>
        new(true, pullRequestNumber, runAttempt, message);
}
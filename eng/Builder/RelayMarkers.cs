namespace WpfReorganize.Builder;

internal static class RelayMarkers
{
    private const string SourcePullRequestTrailer = "Source-PR: ";
    private const string SourceHeadShaTrailer = "Source-Head-SHA: ";

    public static string CreateBranchName(int pullRequestNumber)
    {
        if (pullRequestNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pullRequestNumber));
        }

        return $"t/bot/PR_{pullRequestNumber}";
    }

    public static string CreatePullRequestMarker(PullRequestAddress address) =>
        $"<!-- builder-pr-relay source={address.SourceKey} -->";

    public static string CreateMergeMessage(PullRequestSource source) =>
        $"Merge {source.Address.CanonicalUrl}\n\n{SourcePullRequestTrailer}{source.Address.CanonicalUrl}\n{SourceHeadShaTrailer}{source.HeadSha}";

    public static bool MergeMessageMatches(string message, PullRequestAddress address) =>
        !string.IsNullOrWhiteSpace(message)
        && message.Split('\n').Any(line =>
            string.Equals(line.TrimEnd('\r'), SourcePullRequestTrailer + address.CanonicalUrl, StringComparison.Ordinal));

    public static bool PullRequestBodyMatches(string? body, PullRequestAddress address) =>
        body?.Contains(CreatePullRequestMarker(address), StringComparison.Ordinal) == true;
}

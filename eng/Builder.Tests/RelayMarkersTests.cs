using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class RelayMarkersTests
{
    [Fact]
    public void CreateBranchName_UsesFixedContract()
    {
        Assert.Equal("t/bot/PR_11781", RelayMarkers.CreateBranchName(11781));
    }

    [Fact]
    public void Markers_MatchOnlyExpectedSource()
    {
        var source = CreateSource();
        var message = RelayMarkers.CreateMergeMessage(source);
        var body = GitHubPullRequestService.CreateBody(
            CreateTarget(),
            source,
            DateTimeOffset.Parse("2025-01-02T03:04:05Z"));
        var other = new PullRequestAddress("other", "repo", 11781);

        Assert.True(RelayMarkers.MergeMessageMatches(message, source.Address));
        Assert.True(RelayMarkers.PullRequestBodyMatches(body, source.Address));
        Assert.False(RelayMarkers.MergeMessageMatches(message, other));
        Assert.False(RelayMarkers.PullRequestBodyMatches(body, other));
    }

    private static PullRequestSource CreateSource()
    {
        var sha = GitObjectId.Parse("1111111111111111111111111111111111111111");
        return new PullRequestSource(
            new PullRequestAddress("dotnet", "wpf", 11781),
            "Fix a bug",
            "open",
            false,
            new GitHubRepositoryAddress("contributor", "wpf"),
            "https://github.com/contributor/wpf.git",
            "fix/bug",
            sha,
            new GitHubRepositoryAddress("dotnet", "wpf"),
            "https://github.com/dotnet/wpf.git",
            "main",
            GitObjectId.Parse("0000000000000000000000000000000000000000"),
            new HashSet<GitObjectId> { sha });
    }

    private static TargetRepository CreateTarget() => new(
        "origin",
        new GitHubRepositoryAddress("owner", "wpf"),
        "https://github.com/owner/wpf.git",
        "git@github.com:owner/wpf.git",
        "main",
        "t/bot/PR_11781",
        GitObjectId.Parse("2222222222222222222222222222222222222222"),
        null);
}

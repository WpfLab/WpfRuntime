using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class RelayPullRequestCommandTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveTargetRemote_WhenTargetRemoteIsMissingThenUsesOrigin(string? targetRemote)
    {
        var result = RelayPullRequestCommand.ResolveTargetRemote(targetRemote);

        Assert.Equal("origin", result);
    }

    [Fact]
    public void ResolveBaseBranch_WhenBaseIsMissingThenUsesTargetRemoteMain()
    {
        var result = RelayPullRequestCommand.ResolveBaseBranch(null, "origin");

        Assert.Equal("origin/main", result);
    }

    [Fact]
    public void ResolveBaseBranch_WhenBaseIsProvidedThenUsesProvidedBase()
    {
        var result = RelayPullRequestCommand.ResolveBaseBranch("release/9.0", "origin");

        Assert.Equal("release/9.0", result);
    }

    [Fact]
    public void ResolveGitHubToken_WhenCommandLineTokenIsProvidedThenUsesCommandLineToken()
    {
        var token = RelayPullRequestCommand.ResolveGitHubToken("command-line-token", "environment-token");

        Assert.Equal("command-line-token", token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveGitHubToken_WhenCommandLineTokenIsMissingThenUsesEnvironmentToken(string? commandLineToken)
    {
        var token = RelayPullRequestCommand.ResolveGitHubToken(commandLineToken, "environment-token");

        Assert.Equal("environment-token", token);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void ResolveGitHubToken_WhenNoTokenIsProvidedThenReturnsNull(
        string? commandLineToken,
        string? environmentToken)
    {
        var token = RelayPullRequestCommand.ResolveGitHubToken(commandLineToken, environmentToken);

        Assert.Null(token);
    }
}

using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class RelayPullRequestCommandTests
{
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

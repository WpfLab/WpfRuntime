using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class GitHubRepositoryAddressTests
{
    [Theory]
    [InlineData("https://github.com/dotnet/wpf.git", "dotnet", "wpf")]
    [InlineData("ssh://git@github.com/dotnet/wpf.git", "dotnet", "wpf")]
    [InlineData("git@github.com:dotnet/wpf.git", "dotnet", "wpf")]
    public void ParseRemote_AcceptsSupportedUrls(string value, string owner, string repository)
    {
        var address = GitHubRepositoryAddress.ParseRemote(value);

        Assert.Equal(owner, address.Owner);
        Assert.Equal(repository, address.Repository);
    }

    [Theory]
    [InlineData("http://github.com/dotnet/wpf.git")]
    [InlineData("https://token@github.com/dotnet/wpf.git")]
    [InlineData("https://github.com:444/dotnet/wpf.git")]
    [InlineData("https://example.com/dotnet/wpf.git")]
    [InlineData("https://github.com/dotnet/wpf/extra.git")]
    public void TryParseRemote_RejectsUnsupportedUrls(string value)
    {
        Assert.False(GitHubRepositoryAddress.TryParseRemote(value, out _));
    }

    [Fact]
    public void FromMatchingRemotes_RejectsDifferentRepositories()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GitHubRepositoryAddress.FromMatchingRemotes(
                "https://github.com/dotnet/wpf.git",
                "git@github.com:someone/wpf.git"));
    }
}

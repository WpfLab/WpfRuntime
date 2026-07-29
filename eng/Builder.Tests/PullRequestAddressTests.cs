using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class PullRequestAddressTests
{
    [Theory]
    [InlineData("https://github.com/dotnet/wpf/pull/11781", "dotnet", "wpf", 11781)]
    [InlineData("https://GITHUB.com/dotnet/wpf/pull/11781/files", "dotnet", "wpf", 11781)]
    [InlineData("https://github.com/dotnet/wpf/pull/11781/commits?tab=files", "dotnet", "wpf", 11781)]
    [InlineData("https://github.com/dotnet%2Dcampus/wpf/pull/1", "dotnet-campus", "wpf", 1)]
    public void Parse_AcceptsSupportedUrls(
        string value,
        string owner,
        string repository,
        int number)
    {
        var address = PullRequestAddress.Parse(value);

        Assert.Equal(owner, address.Owner);
        Assert.Equal(repository, address.Repository);
        Assert.Equal(number, address.Number);
        Assert.Equal($"https://github.com/{owner}/{repository}/pull/{number}", address.CanonicalUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://github.com/dotnet/wpf/pull/1")]
    [InlineData("https://example.com/dotnet/wpf/pull/1")]
    [InlineData("https://user@github.com/dotnet/wpf/pull/1")]
    [InlineData("https://github.com:444/dotnet/wpf/pull/1")]
    [InlineData("https://github.com/dotnet/wpf/issues/1")]
    [InlineData("https://github.com/dotnet/wpf/pull/0")]
    [InlineData("https://github.com/dotnet/wpf/pull/not-a-number")]
    [InlineData("https://github.com/dotnet%ZZ/wpf/pull/1")]
    [InlineData("https://github.com/dotnet%2Fwpf/repo/pull/1")]
    public void TryParse_RejectsUnsupportedUrls(string? value)
    {
        Assert.False(PullRequestAddress.TryParse(value, out _));
    }
}

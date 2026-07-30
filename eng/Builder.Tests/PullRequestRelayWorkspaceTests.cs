using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class PullRequestRelayWorkspaceTests
{
    [Fact]
    public void Open_WhenPathIsRelativeThenReturnsFullWorkspacePath()
    {
        var workspace = PullRequestRelayWorkspace.Open("relay-workspace");

        Assert.Equal(Path.GetFullPath("relay-workspace"), workspace.RootPath);
    }

    [Fact]
    public async Task Create_WhenCreatedThenUsesShortWorkspacePath()
    {
        var workspace = PullRequestRelayWorkspace.Create(new PullRequestAddress("dotnet", "wpf", 11124));
        await workspace.WriteStateAsync
        (
            new PullRequestRelayState
            {
                Stage = PullRequestRelayStage.InputValidated,
            },
            CancellationToken.None
        );
        try
        {
            Assert.Matches(@"WpfRuntimeTemp[\\/]wpf-11124-\d{10}$", workspace.RootPath);
        }
        finally
        {
            workspace.Delete();
        }
    }
}

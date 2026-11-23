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
    public void Create_WhenRootIsRelativeThenUsesThatParent()
    {
        var parent = Path.GetRelativePath(Environment.CurrentDirectory, Path.Join(Path.GetTempPath(), $"relay-{Guid.NewGuid():N}"));
        var workspace = PullRequestRelayWorkspace.Create(new PullRequestAddress("dotnet", "wpf", 11124), parent);

        Assert.Equal(Path.GetFullPath(parent), Path.GetDirectoryName(workspace.RootPath));
    }

    [Fact]
    public void Create_WhenRelayingSamePullRequestTwiceThenUsesDifferentDirectories()
    {
        var parent = Path.Join(Path.GetTempPath(), $"relay-{Guid.NewGuid():N}");
        var source = new PullRequestAddress("dotnet", "wpf", 11124);

        var first = PullRequestRelayWorkspace.Create(source, parent);
        var second = PullRequestRelayWorkspace.Create(source, parent);

        Assert.NotEqual(first.RootPath, second.RootPath);
    }

    [Fact]
    public async Task ReadStateAsync_WhenCustomWorkspaceIsReopenedThenRestoresRetentionSetting()
    {
        var parent = Path.Join(Path.GetTempPath(), $"relay-{Guid.NewGuid():N}");
        var workspace = PullRequestRelayWorkspace.Create(new PullRequestAddress("dotnet", "wpf", 11124), parent);
        await workspace.WriteStateAsync(new PullRequestRelayState { PreserveWorkspace = true }, CancellationToken.None);

        var state = await PullRequestRelayWorkspace.Open(workspace.RootPath).ReadStateAsync(CancellationToken.None);

        Assert.True(state.PreserveWorkspace);
    }

    [Fact]
    public async Task Delete_WhenCustomWorkspaceIsReopenedThenRemovesWorkspace()
    {
        var parent = Path.Join(Path.GetTempPath(), $"relay-{Guid.NewGuid():N}");
        var workspace = PullRequestRelayWorkspace.Create(new PullRequestAddress("dotnet", "wpf", 11124), parent);
        await workspace.WriteStateAsync(new PullRequestRelayState(), CancellationToken.None);

        PullRequestRelayWorkspace.Open(workspace.RootPath).Delete();

        Assert.False(Directory.Exists(workspace.RootPath));
    }

    [Fact]
    public async Task Delete_WhenCustomParentContainsStateButIsNotAWorkspaceThenRefusesToRemoveIt()
    {
        var parent = Path.Join(Path.GetTempPath(), $"relay-{Guid.NewGuid():N}");
        PullRequestRelayWorkspace.Create(new PullRequestAddress("dotnet", "wpf", 11124), parent);
        var parentAsWorkspace = PullRequestRelayWorkspace.Open(parent);
        await parentAsWorkspace.WriteStateAsync(new PullRequestRelayState(), CancellationToken.None);

        Assert.Throws<InvalidOperationException>(parentAsWorkspace.Delete);
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
            Assert.Matches(@"WpfRuntimeTemp[\\/]wpf-11124-\d{10}-[0-9a-f]{12}$", workspace.RootPath);
        }
        finally
        {
            workspace.Delete();
        }
    }
}

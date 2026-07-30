using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class GitServiceIntegrationTests
{
    [Theory]
    [InlineData(null, "main")]
    [InlineData("", "main")]
    [InlineData("main", "main")]
    [InlineData("origin/main", "main")]
    [InlineData("release/9.0", "release/9.0")]
    public void ResolveBaseBranch_NormalizesTargetRemotePrefix(string? requestedBaseBranch, string expected)
    {
        var result = GitService.ResolveBaseBranch("origin", requestedBaseBranch);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ApplyAndPush_CreatesRelayCommitAndPushesExactSha()
    {
        using var repository = await GitTestRepository.CreateAsync();
        var service = repository.CreateService();
        var target = repository.CreateTarget();
        var source = repository.CreateSource();
        var workspace = repository.CreateWorkspace();
        try
        {
            await service.CloneTargetAsync(repository.CallerPath, target, workspace, CancellationToken.None);
            await service.FetchSourceAsync(source, workspace, CancellationToken.None);
            var relayCommit = await service.ApplySourceChangesAsync(source, target, workspace, ConflictMode.Fail, CancellationToken.None);

            Assert.NotEqual(source.HeadSha, relayCommit);
            await service.PushValidatedCommitAsync(target, relayCommit, workspace, CancellationToken.None);
            Assert.Equal(relayCommit, await repository.GetRemoteBranchShaAsync(target.RelayBranch));
        }
        finally
        {
            workspace.Delete();
        }
    }

    [Fact]
    public async Task ApplySourceChanges_PreservesSourceAuthorAndUsesBotCommitter()
    {
        using var repository = await GitTestRepository.CreateAsync();
        var service = repository.CreateService();
        var target = repository.CreateTarget();
        var source = repository.CreateSource();
        var workspace = repository.CreateWorkspace();
        try
        {
            await service.CloneTargetAsync(repository.CallerPath, target, workspace, CancellationToken.None);
            await service.FetchSourceAsync(source, workspace, CancellationToken.None);
            var relayCommit = await service.ApplySourceChangesAsync(source, target, workspace, ConflictMode.Fail, CancellationToken.None);

            var identity = await repository.GetCommitFormatAsync
            (
                workspace.RepositoryPath,
                relayCommit,
                "%an <%ae>|%cn <%ce>"
            );
            Assert.Equal("Source Author <source-author@example.com>|WpfRuntime Bot <wpfruntime-bot@users.noreply.github.com>", identity);
        }
        finally
        {
            workspace.Delete();
        }
    }

    [Fact]
    public async Task FetchSource_FallsBackToFixedPullRequestRefWhenBranchMoves()
    {
        using var repository = await GitTestRepository.CreateAsync();
        var fixedHead = repository.SourceSha;
        await repository.SetPullRequestRefAsync(fixedHead);
        await repository.CommitOnSourceAsync("moved.txt", "new branch head", "move branch");
        var service = repository.CreateService();
        var target = repository.CreateTarget();
        var source = repository.CreateSource(
            new HashSet<GitObjectId> { fixedHead },
            headSha: fixedHead);
        var workspace = repository.CreateWorkspace();
        try
        {
            await service.CloneTargetAsync(repository.CallerPath, target, workspace, CancellationToken.None);
            await service.FetchSourceAsync(source, workspace, CancellationToken.None);
            var relayCommit = await service.ApplySourceChangesAsync(source, target, workspace, ConflictMode.Fail, CancellationToken.None);

            Assert.NotEqual(source.HeadSha, relayCommit);
        }
        finally
        {
            workspace.Delete();
        }
    }

    [Fact]
    public async Task ApplySourceChanges_WhenSourceBaseAdvanced_ExcludesBaseOnlyChanges()
    {
        using var repository = await GitTestRepository.CreateAsync();
        var sourceHead = repository.SourceSha;
        var currentSourceBase = await repository.AdvanceSourceBaseAsync(
            "base-only.txt",
            "base-only change",
            "advance source base");
        var service = repository.CreateService();
        var target = repository.CreateTarget();
        var source = repository.CreateSource(
            new HashSet<GitObjectId> { sourceHead },
            baseSha: currentSourceBase,
            headSha: sourceHead);
        var workspace = repository.CreateWorkspace();
        try
        {
            await service.CloneTargetAsync(repository.CallerPath, target, workspace, CancellationToken.None);
            await service.FetchSourceAsync(source, workspace, CancellationToken.None);

            await service.ApplySourceChangesAsync(source, target, workspace, ConflictMode.Fail, CancellationToken.None);

            Assert.False(File.Exists(Path.Join(workspace.RepositoryPath, "base-only.txt")));
        }
        finally
        {
            workspace.Delete();
        }
    }

    [Fact]
    public async Task ApplySourceChanges_AbortsAndReportsConflict()
    {
        using var repository = await GitTestRepository.CreateAsync();
        var sourceBase = repository.BaseSha;
        var firstSourceCommit = repository.SourceSha;
        var conflictingSourceCommit = await repository.CommitOnSourceAsync(
            "base.txt",
            "source change",
            "source conflict");
        await repository.CommitOnTargetMainAsync("base.txt", "target change", "target conflict");
        var service = repository.CreateService();
        var target = repository.CreateTarget();
        var source = repository.CreateSource(
            new HashSet<GitObjectId> { firstSourceCommit, conflictingSourceCommit },
            sourceBase);
        var workspace = repository.CreateWorkspace();
        try
        {
            await service.CloneTargetAsync(repository.CallerPath, target, workspace, CancellationToken.None);
            await service.FetchSourceAsync(source, workspace, CancellationToken.None);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ApplySourceChangesAsync(source, target, workspace, ConflictMode.Fail, CancellationToken.None));
            Assert.Contains("base.txt", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await service.GetTrackedChangesAsync(
                workspace.RepositoryPath,
                workspace.IsolatedHomePath,
                CancellationToken.None));
        }
        finally
        {
            workspace.Delete();
        }
    }

    [Fact]
    public async Task ApplySourceChanges_WhenSourceBaseDiverged_AppliesPullRequestDiff()
    {
        using var repository = await GitTestRepository.CreateAsync();
        var sourceBase = await repository.CreateDivergedSourceHistoryAsync("ported.txt", "ported change");
        var service = repository.CreateService();
        var target = repository.CreateTarget();
        var source = repository.CreateSource(
            new HashSet<GitObjectId> { repository.SourceSha },
            sourceBase);
        var workspace = repository.CreateWorkspace();
        try
        {
            await service.CloneTargetAsync(repository.CallerPath, target, workspace, CancellationToken.None);
            await service.FetchSourceAsync(source, workspace, CancellationToken.None);

            await service.ApplySourceChangesAsync(source, target, workspace, ConflictMode.Fail, CancellationToken.None);

            Assert.Equal("ported change", File.ReadAllText(Path.Join(workspace.RepositoryPath, "ported.txt")));
        }
        finally
        {
            workspace.Delete();
        }
    }

    [Fact]
    public async Task PushValidatedCommit_RejectsLeaseCompetition()
    {
        using var repository = await GitTestRepository.CreateAsync();
        var service = repository.CreateService();
        var firstTarget = repository.CreateTarget();
        var source = repository.CreateSource();
        var firstWorkspace = repository.CreateWorkspace();
        GitObjectId firstRelayCommit;
        try
        {
            await service.CloneTargetAsync(repository.CallerPath, firstTarget, firstWorkspace, CancellationToken.None);
            await service.FetchSourceAsync(source, firstWorkspace, CancellationToken.None);
            firstRelayCommit = await service.ApplySourceChangesAsync(source, firstTarget, firstWorkspace, ConflictMode.Fail, CancellationToken.None);
            await service.PushValidatedCommitAsync(firstTarget, firstRelayCommit, firstWorkspace, CancellationToken.None);
        }
        finally
        {
            firstWorkspace.Delete();
        }

        var staleTarget = repository.CreateTarget(firstRelayCommit);
        var secondWorkspace = repository.CreateWorkspace();
        try
        {
            await service.CloneTargetAsync(repository.CallerPath, staleTarget, secondWorkspace, CancellationToken.None);
            await service.FetchSourceAsync(source, secondWorkspace, CancellationToken.None);
            var secondRelayCommit = await service.ApplySourceChangesAsync(source, staleTarget, secondWorkspace, ConflictMode.Fail, CancellationToken.None);
            await repository.AdvanceRemoteRelayBranchAsync(staleTarget.RelayBranch);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.PushValidatedCommitAsync(staleTarget, secondRelayCommit, secondWorkspace, CancellationToken.None));
        }
        finally
        {
            secondWorkspace.Delete();
        }
    }
}

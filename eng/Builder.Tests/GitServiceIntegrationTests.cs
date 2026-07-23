using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class GitServiceIntegrationTests
{
    [Fact]
    public async Task MergeAndPush_CreatesNoFastForwardMergeAndPushesExactSha()
    {
        using var repository = await GitTestRepository.CreateAsync();
        var service = repository.CreateService();
        var target = repository.CreateTarget();
        var source = repository.CreateSource();
        var workspace = repository.CreateWorkspace();
        try
        {
            await service.CloneTargetAsync(target, workspace, CancellationToken.None);
            await service.FetchSourceAsync(source, workspace, CancellationToken.None);
            var mergedCommit = await service.MergeSourceAsync(source, target, workspace, CancellationToken.None);

            Assert.NotEqual(source.HeadSha, mergedCommit);
            await service.PushValidatedCommitAsync(target, mergedCommit, workspace, CancellationToken.None);
            Assert.Equal(mergedCommit, await repository.GetRemoteBranchShaAsync(target.RelayBranch));
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
            await service.CloneTargetAsync(target, workspace, CancellationToken.None);
            await service.FetchSourceAsync(source, workspace, CancellationToken.None);
            var mergedCommit = await service.MergeSourceAsync(source, target, workspace, CancellationToken.None);

            Assert.NotEqual(source.HeadSha, mergedCommit);
        }
        finally
        {
            workspace.Delete();
        }
    }

    [Fact]
    public async Task MergeSource_AbortsAndReportsConflictFiles()
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
            await service.CloneTargetAsync(target, workspace, CancellationToken.None);
            await service.FetchSourceAsync(source, workspace, CancellationToken.None);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.MergeSourceAsync(source, target, workspace, CancellationToken.None));
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
    public async Task MergeSource_RejectsCommitsOutsidePullRequestSet()
    {
        using var repository = await GitTestRepository.CreateAsync();
        var service = repository.CreateService();
        var target = repository.CreateTarget();
        await repository.CommitOnSourceAsync("extra.txt", "extra", "extra");
        var source = repository.CreateSource(new HashSet<GitObjectId> { repository.SourceSha });
        var workspace = repository.CreateWorkspace();
        try
        {
            await service.CloneTargetAsync(target, workspace, CancellationToken.None);
            await service.FetchSourceAsync(source, workspace, CancellationToken.None);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.MergeSourceAsync(source, target, workspace, CancellationToken.None));
            Assert.Contains("outside the source pull request", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            workspace.Delete();
        }
    }

    [Fact]
    public async Task MergeSource_RejectsUnrelatedHistories()
    {
        using var repository = await GitTestRepository.CreateAsync();
        await repository.CreateUnrelatedSourceHistoryAsync();
        var service = repository.CreateService();
        var target = repository.CreateTarget();
        var source = repository.CreateSource(
            new HashSet<GitObjectId> { repository.SourceSha },
            repository.BaseSha);
        var workspace = repository.CreateWorkspace();
        try
        {
            await service.CloneTargetAsync(target, workspace, CancellationToken.None);
            await service.FetchSourceAsync(source, workspace, CancellationToken.None);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.MergeSourceAsync(source, target, workspace, CancellationToken.None));
            Assert.Contains("share Git history", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        GitObjectId firstMerged;
        try
        {
            await service.CloneTargetAsync(firstTarget, firstWorkspace, CancellationToken.None);
            await service.FetchSourceAsync(source, firstWorkspace, CancellationToken.None);
            firstMerged = await service.MergeSourceAsync(source, firstTarget, firstWorkspace, CancellationToken.None);
            await service.PushValidatedCommitAsync(firstTarget, firstMerged, firstWorkspace, CancellationToken.None);
        }
        finally
        {
            firstWorkspace.Delete();
        }

        var staleTarget = repository.CreateTarget(firstMerged);
        var secondWorkspace = repository.CreateWorkspace();
        try
        {
            await service.CloneTargetAsync(staleTarget, secondWorkspace, CancellationToken.None);
            await service.FetchSourceAsync(source, secondWorkspace, CancellationToken.None);
            var secondMerged = await service.MergeSourceAsync(source, staleTarget, secondWorkspace, CancellationToken.None);
            await repository.AdvanceRemoteRelayBranchAsync(staleTarget.RelayBranch);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.PushValidatedCommitAsync(staleTarget, secondMerged, secondWorkspace, CancellationToken.None));
        }
        finally
        {
            secondWorkspace.Delete();
        }
    }
}

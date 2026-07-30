using System.Diagnostics;
using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

internal sealed class GitTestRepository : IDisposable
{
    private readonly string _gitPath;

    private GitTestRepository(string rootPath, string gitPath)
    {
        RootPath = rootPath;
        _gitPath = gitPath;
        TargetBarePath = Path.Join(rootPath, "target.git");
        SourceBarePath = Path.Join(rootPath, "source.git");
        SeedPath = Path.Join(rootPath, "seed");
        CallerPath = Path.Join(rootPath, "caller");
    }

    public string RootPath { get; }

    public string TargetBarePath { get; }

    public string SourceBarePath { get; }

    public string SeedPath { get; }

    public string CallerPath { get; }

    public GitObjectId BaseSha { get; private set; }

    public GitObjectId SourceSha { get; private set; }

    public TargetRepository CreateTarget(GitObjectId? existingRelayBranchSha = null) =>
        new(
            "origin",
            new GitHubRepositoryAddress("target", "wpf"),
            TargetBarePath,
            TargetBarePath,
            "main",
            RelayMarkers.CreateBranchName(42),
            BaseSha,
            existingRelayBranchSha);

    public static async Task<GitTestRepository> CreateAsync()
    {
        var rootPath = Path.Join(Path.GetTempPath(), $"builder-git-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        var gitPath = await GitService.FindGitAsync(rootPath, CancellationToken.None);
        var repository = new GitTestRepository(rootPath, gitPath);
        await repository.InitializeAsync();
        return repository;
    }

    public GitService CreateService(TimeSpan? timeout = null) => new(_gitPath, timeout);

    public PullRequestSource CreateSource
    (
        IReadOnlySet<GitObjectId>? commitShas = null,
        GitObjectId? baseSha = null,
        GitObjectId? headSha = null,
        string? headCloneUrl = null,
        string title = "Test source"
    ) =>
        new
        (
            new PullRequestAddress("dotnet", "wpf", 42),
            title,
            "open",
            false,
            new GitHubRepositoryAddress("source", "wpf"),
            headCloneUrl ?? SourceBarePath,
            "feature",
            headSha ?? SourceSha,
            new GitHubRepositoryAddress("dotnet", "wpf"),
            SourceBarePath,
            "main",
            baseSha ?? BaseSha,
            commitShas ?? new HashSet<GitObjectId> { headSha ?? SourceSha },
            "Source Author",
            "source-author@example.com"
        );

    public PullRequestRelayWorkspace CreateWorkspace()
    {
        var workspace = PullRequestRelayWorkspace.Create(new PullRequestAddress("dotnet", "wpf", 42));
        workspace.WriteStateAsync(
            new PullRequestRelayState
            {
                Stage = PullRequestRelayStage.InputValidated,
                SourcePullRequestUrl = "https://github.com/dotnet/wpf/pull/42",
            },
            CancellationToken.None).GetAwaiter().GetResult();
        return workspace;
    }

    public async Task<GitObjectId> CommitOnSourceAsync(string fileName, string content, string message)
    {
        await RunAsync(SeedPath, "switch", "feature");
        File.WriteAllText(Path.Join(SeedPath, fileName), content);
        await RunAsync(SeedPath, "add", fileName);
        await RunAsync(SeedPath, "commit", "-m", message);
        await RunAsync(SeedPath, "push", "source", "feature");
        SourceSha = await ResolveAsync(SeedPath, "HEAD");
        return SourceSha;
    }

    public async Task<GitObjectId> CommitOnTargetMainAsync(string fileName, string content, string message)
    {
        await RunAsync(SeedPath, "switch", "main");
        File.WriteAllText(Path.Join(SeedPath, fileName), content);
        await RunAsync(SeedPath, "add", fileName);
        await RunAsync(SeedPath, "commit", "-m", message);
        await RunAsync(SeedPath, "push", "target", "main");
        BaseSha = await ResolveAsync(SeedPath, "HEAD");
        return BaseSha;
    }

    public async Task<GitObjectId> AdvanceSourceBaseAsync(string fileName, string content, string message)
    {
        await RunAsync(SeedPath, "switch", "main");
        File.WriteAllText(Path.Join(SeedPath, fileName), content);
        await RunAsync(SeedPath, "add", fileName);
        await RunAsync(SeedPath, "commit", "-m", message);
        await RunAsync(SeedPath, "push", "source", "main");
        return await ResolveAsync(SeedPath, "HEAD");
    }

    public Task SetPullRequestRefAsync(GitObjectId sha) =>
        RunAsync(
            RootPath,
            "--git-dir",
            SourceBarePath,
            "update-ref",
            "refs/pull/42/head",
            sha.ToString());

    public async Task<GitObjectId> CreateUnrelatedSourceHistoryAsync()
    {
        var unrelatedPath = Path.Join(RootPath, "unrelated");
        Directory.CreateDirectory(unrelatedPath);
        await RunAsync(unrelatedPath, "init", "--initial-branch=feature");
        await ConfigureIdentityAsync(unrelatedPath);
        File.WriteAllText(Path.Join(unrelatedPath, "unrelated.txt"), "unrelated");
        await RunAsync(unrelatedPath, "add", "unrelated.txt");
        await RunAsync(unrelatedPath, "commit", "-m", "unrelated");
        await RunAsync(unrelatedPath, "remote", "add", "source", SourceBarePath);
        await RunAsync(unrelatedPath, "push", "--force", "source", "feature");
        SourceSha = await ResolveAsync(unrelatedPath, "HEAD");
        return SourceSha;
    }

    public async Task<GitObjectId> CreateDivergedSourceHistoryAsync(
        string fileName,
        string content)
    {
        await RunAsync(SeedPath, "switch", "main");
        File.WriteAllText(Path.Join(SeedPath, "source-base.txt"), "source base");
        await RunAsync(SeedPath, "add", "source-base.txt");
        await RunAsync(SeedPath, "commit", "-m", "source base");
        var sourceBase = await ResolveAsync(SeedPath, "HEAD");
        await RunAsync(SeedPath, "push", "--force", "source", "main");
        await RunAsync(SeedPath, "switch", "-C", "feature");
        File.WriteAllText(Path.Join(SeedPath, fileName), content);
        await RunAsync(SeedPath, "add", fileName);
        await RunAsync(SeedPath, "commit", "-m", "diverged feature");
        await RunAsync(SeedPath, "push", "--force", "source", "feature");
        SourceSha = await ResolveAsync(SeedPath, "HEAD");
        return sourceBase;
    }

    public async Task<GitObjectId> GetRemoteBranchShaAsync(string branch)
    {
        var result = await RunAsync(RootPath, "ls-remote", "--heads", TargetBarePath, $"refs/heads/{branch}");
        var sha = result.StandardOutput
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        return GitObjectId.Parse(sha);
    }

    public async Task<string> GetCommitFormatAsync
    (
        string repositoryPath,
        GitObjectId commit,
        string format
    )
    {
        var result = await RunAsync
        (
            repositoryPath,
            "show",
            "--no-patch",
            $"--format={format}",
            commit.ToString()
        );
        return result.StandardOutput.Trim();
    }

    public async Task AdvanceRemoteRelayBranchAsync(string branch)
    {
        var updater = Path.Join(RootPath, $"updater-{Guid.NewGuid():N}");
        await RunAsync(RootPath, "clone", TargetBarePath, updater);
        await ConfigureIdentityAsync(updater);
        await RunAsync(updater, "switch", branch);
        File.WriteAllText(Path.Join(updater, "racer.txt"), Guid.NewGuid().ToString("N"));
        await RunAsync(updater, "add", "racer.txt");
        await RunAsync(updater, "commit", "-m", "race update");
        await RunAsync(updater, "push", "origin", branch);
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            foreach (var filePath in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.ReadOnly);
            }
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private async Task InitializeAsync()
    {
        await RunAsync(RootPath, "init", "--bare", "--initial-branch=main", TargetBarePath);
        await RunAsync(RootPath, "init", "--bare", "--initial-branch=main", SourceBarePath);
        Directory.CreateDirectory(SeedPath);
        await RunAsync(SeedPath, "init", "--initial-branch=main");
        await ConfigureIdentityAsync(SeedPath);
        File.WriteAllText(Path.Join(SeedPath, "base.txt"), "base");
        await RunAsync(SeedPath, "add", "base.txt");
        await RunAsync(SeedPath, "commit", "-m", "base");
        BaseSha = await ResolveAsync(SeedPath, "HEAD");
        await RunAsync(SeedPath, "remote", "add", "target", TargetBarePath);
        await RunAsync(SeedPath, "remote", "add", "source", SourceBarePath);
        await RunAsync(SeedPath, "push", "target", "main");
        await RunAsync(SeedPath, "push", "source", "main");
        await RunAsync(SeedPath, "switch", "-c", "feature");
        File.WriteAllText(Path.Join(SeedPath, "feature.txt"), "feature");
        await RunAsync(SeedPath, "add", "feature.txt");
        await RunAsync(SeedPath, "commit", "-m", "feature");
        SourceSha = await ResolveAsync(SeedPath, "HEAD");
        await RunAsync(SeedPath, "push", "source", "feature");
        await RunAsync(RootPath, "clone", TargetBarePath, CallerPath);
        await RunAsync(CallerPath, "remote", "set-url", "--push", "origin", TargetBarePath);
    }

    private async Task ConfigureIdentityAsync(string repositoryPath)
    {
        await RunAsync(repositoryPath, "config", "user.name", "Builder Tests");
        await RunAsync(repositoryPath, "config", "user.email", "builder-tests@example.invalid");
    }

    private async Task<GitObjectId> ResolveAsync(string repositoryPath, string revision)
    {
        var result = await RunAsync(repositoryPath, "rev-parse", $"{revision}^{{commit}}");
        return GitObjectId.Parse(result.StandardOutput.Trim());
    }

    private async Task<ProcessResult> RunAsync(string workingDirectory, params string[] arguments)
    {
        var result = await ProcessRunner.RunAsync(
            new ProcessRunOptions(_gitPath, workingDirectory, arguments)
            {
                Timeout = TimeSpan.FromMinutes(1),
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    ["GIT_TERMINAL_PROMPT"] = "0",
                    ["GCM_INTERACTIVE"] = "Never",
                },
            });
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {result.Output}");
        }

        return result;
    }
}

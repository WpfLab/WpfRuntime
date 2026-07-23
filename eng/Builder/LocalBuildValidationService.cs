namespace WpfReorganize.Builder;

internal sealed record LocalBuildValidationResult(
    GitObjectId CommitSha,
    GitObjectId TreeSha,
    string PackagePath,
    DateTimeOffset CompletedAtUtc);

internal sealed class LocalBuildValidationService
{
    private static readonly TimeSpan RestoreTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan BuilderBuildTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan FullBuildTimeout = TimeSpan.FromHours(3);
    private static readonly TimeSpan PackageTestTimeout = TimeSpan.FromHours(1);
    private readonly GitService _git;
    private readonly string _dotnetPath;
    private readonly string _msBuildPath;

    public LocalBuildValidationService(GitService git, string dotnetPath, string msBuildPath)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentException.ThrowIfNullOrWhiteSpace(dotnetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(msBuildPath);
        _git = git;
        _dotnetPath = dotnetPath;
        _msBuildPath = msBuildPath;
    }

    public static async Task<string> FindDotNetAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            new ProcessRunOptions("where.exe", workingDirectory, "dotnet.exe")
            {
                Timeout = TimeSpan.FromSeconds(30),
            },
            cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("dotnet.exe was not found on PATH.");
        }

        var path = result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(File.Exists);
        return path ?? throw new InvalidOperationException("dotnet.exe was not found on PATH.");
    }

    public async Task<LocalBuildValidationResult> ValidateAsync(
        PullRequestSource source,
        TargetRepository target,
        GitObjectId mergedCommit,
        PullRequestRelayWorkspace workspace,
        PullRequestRelayState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(state);
        var repositoryPath = workspace.RepositoryPath;
        var initialHead = await _git.ResolveCommitAsync(
            repositoryPath,
            workspace.IsolatedHomePath,
            "HEAD",
            cancellationToken).ConfigureAwait(false);
        if (initialHead != mergedCommit)
        {
            throw new InvalidOperationException($"HEAD {initialHead} does not match merged commit {mergedCommit}.");
        }

        var initialTree = await _git.ResolveTreeAsync(
            repositoryPath,
            workspace.IsolatedHomePath,
            "HEAD",
            cancellationToken).ConfigureAwait(false);
        state.MergedTreeSha = initialTree.ToString();
        await workspace.WriteStateAsync(state, cancellationToken).ConfigureAwait(false);
        var initialChanges = await _git.GetTrackedChangesAsync(
            repositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken).ConfigureAwait(false);
        if (initialChanges.Count > 0)
        {
            throw new InvalidOperationException(
                $"Tracked working tree or index is dirty before validation: {string.Join(", ", initialChanges)}");
        }

        var builderProject = Path.Join(repositoryPath, "eng", "Builder", "Builder.csproj");
        var solutionPath = Path.Join(repositoryPath, "Microsoft.Dotnet.Wpf.slnx");
        RequireFile(builderProject);
        RequireFile(solutionPath);
        var nupkgDirectory = Path.Join(repositoryPath, "eng", "Builder", "bin", "nupkg");
        if (Directory.Exists(nupkgDirectory))
        {
            Directory.Delete(nupkgDirectory, recursive: true);
        }
        Directory.CreateDirectory(nupkgDirectory);

        var version = $"0.0.0-pr.{source.Address.Number}.sha{source.HeadSha.Short}";
        var packagePath = Path.Join(nupkgDirectory, $"DotNetCampus.WpfLib.{version}.nupkg");
        var environment = ProcessEnvironment.CreateUntrustedBuildEnvironment(workspace.IsolatedHomePath);
        await RunGateAsync(
            "restore-builder",
            _dotnetPath,
            repositoryPath,
            workspace,
            state,
            environment,
            RestoreTimeout,
            cancellationToken,
            "restore",
            builderProject).ConfigureAwait(false);
        await RunGateAsync(
            "build-builder",
            _dotnetPath,
            repositoryPath,
            workspace,
            state,
            environment,
            BuilderBuildTimeout,
            cancellationToken,
            "build",
            builderProject,
            "--no-restore").ConfigureAwait(false);
        await RunGateAsync(
            "build-package",
            _dotnetPath,
            repositoryPath,
            workspace,
            state,
            environment,
            FullBuildTimeout,
            cancellationToken,
            "run",
            "--project",
            builderProject,
            "--no-build",
            "--",
            "--version",
            version).ConfigureAwait(false);
        RequireNonEmptyFile(packagePath);
        await RunGateAsync(
            "test-package",
            _dotnetPath,
            repositoryPath,
            workspace,
            state,
            environment,
            PackageTestTimeout,
            cancellationToken,
            "run",
            "--project",
            builderProject,
            "--no-build",
            "--",
            "test-package",
            "--package",
            packagePath).ConfigureAwait(false);
        await RunGateAsync(
            "rebuild-solution",
            _msBuildPath,
            repositoryPath,
            workspace,
            state,
            environment,
            FullBuildTimeout,
            cancellationToken,
            solutionPath,
            "-restore",
            "/t:Rebuild",
            "/p:Configuration=Debug",
            "/p:Platform=x64",
            "/m:1",
            "/nr:false",
            "/v:minimal").ConfigureAwait(false);

        var finalHead = await _git.ResolveCommitAsync(
            repositoryPath,
            workspace.IsolatedHomePath,
            "HEAD",
            cancellationToken).ConfigureAwait(false);
        var finalTree = await _git.ResolveTreeAsync(
            repositoryPath,
            workspace.IsolatedHomePath,
            "HEAD",
            cancellationToken).ConfigureAwait(false);
        var finalChanges = await _git.GetTrackedChangesAsync(
            repositoryPath,
            workspace.IsolatedHomePath,
            cancellationToken).ConfigureAwait(false);
        if (finalHead != initialHead || finalTree != initialTree || finalChanges.Count > 0)
        {
            throw new InvalidOperationException(
                $"Untrusted validation modified Git state. " +
                $"HEAD: {initialHead} -> {finalHead}; tree: {initialTree} -> {finalTree}; " +
                $"tracked changes: {string.Join(", ", finalChanges)}");
        }

        RequireNonEmptyFile(packagePath);
        return new LocalBuildValidationResult(
            finalHead,
            finalTree,
            packagePath,
            DateTimeOffset.UtcNow);
    }

    private static async Task RunGateAsync(
        string name,
        string fileName,
        string workingDirectory,
        PullRequestRelayWorkspace workspace,
        PullRequestRelayState state,
        IReadOnlyDictionary<string, string?> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var logPath = Path.Join(workspace.LogsPath, $"{name}.log");
        ProcessResult result;
        try
        {
            result = await ProcessRunner.RunAsync(
                new ProcessRunOptions(fileName, workingDirectory, arguments)
                {
                    Timeout = timeout,
                    InheritEnvironment = false,
                    EnvironmentVariables = environment,
                    LogPath = logPath,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await RecordGateAsync(state, workspace, name, logPath, startedAtUtc, exitCode: -1)
                .ConfigureAwait(false);
            throw;
        }
        catch (TimeoutException)
        {
            await RecordGateAsync(state, workspace, name, logPath, startedAtUtc, exitCode: -2)
                .ConfigureAwait(false);
            throw;
        }

        await RecordGateAsync(state, workspace, name, logPath, startedAtUtc, result.ExitCode)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Local validation gate '{name}' failed with exit code {result.ExitCode}. See {logPath}");
        }
    }

    private static async Task RecordGateAsync(
        PullRequestRelayState state,
        PullRequestRelayWorkspace workspace,
        string name,
        string logPath,
        DateTimeOffset startedAtUtc,
        int exitCode)
    {
        state.ValidationGates.Add(new LocalValidationGateState
        {
            Name = name,
            LogPath = logPath,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            ExitCode = exitCode,
        });
        await workspace.WriteStateAsync(state, CancellationToken.None).ConfigureAwait(false);
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required validation input was not found: {path}", path);
        }
    }

    private static void RequireNonEmptyFile(string path)
    {
        RequireFile(path);
        if (new FileInfo(path).Length == 0)
        {
            throw new InvalidOperationException($"Required validation output is empty: {path}");
        }
    }
}

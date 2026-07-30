namespace WpfReorganize.Builder;

internal sealed class GitHubActionsBuildService
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RestoreTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan BuilderBuildTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan FullBuildTimeout = TimeSpan.FromHours(3);
    private static readonly TimeSpan PackageTestTimeout = TimeSpan.FromHours(1);
    private readonly string _gitPath;
    private readonly string _dotnetPath;
    private readonly string _msBuildPath;

    public GitHubActionsBuildService(string gitPath, string dotnetPath, string msBuildPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gitPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dotnetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(msBuildPath);
        _gitPath = gitPath;
        _dotnetPath = dotnetPath;
        _msBuildPath = msBuildPath;
    }

    public async Task<GitHubActionsBuildIdentity> RunAsync(
        GitHubActionsBuildOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var repositoryPath = Path.GetFullPath(options.RepositoryPath);
        RequireDirectory(repositoryPath);
        RequireFile(Path.Join(repositoryPath, "Microsoft.Dotnet.Wpf.slnx"));
        RequireFile(Path.Join(repositoryPath, "eng", "Builder", "Builder.csproj"));

        await ValidateCheckoutCredentialsAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        var testedSha = await ReadTestedShaAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        await ValidateTestedIdentityAsync(
                repositoryPath,
                options.Metadata,
                testedSha,
                cancellationToken)
            .ConfigureAwait(false);
        var initialTree = await ReadTreeShaAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        var initialChanges = await ReadTrackedChangesAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        if (initialChanges.Count > 0)
        {
            throw new InvalidOperationException(
                $"The tested checkout is dirty before the build: {string.Join(", ", initialChanges)}");
        }
        var identity = GitHubActionsBuildIdentity.Create(
            options.Metadata,
            repositoryPath,
            testedSha.ToString(),
            options.RunId,
            options.RunAttempt);
        LogIdentity(options.Metadata, identity);

        var isolatedHome = Path.Join(
            Path.GetTempPath(),
            "WpfReorganize.Builder",
            "ci",
            Guid.NewGuid().ToString("N"));
        var environment = ProcessEnvironment.CreateUntrustedBuildEnvironment(isolatedHome);
        try
        {
            if (options.Target == GitHubActionsBuildTarget.Solution)
            {
                await RebuildSolutionAsync(repositoryPath, environment, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await BuildAndTestPackageAsync(repositoryPath, identity, environment, cancellationToken)
                    .ConfigureAwait(false);
            }

            await ValidateGitStateAfterBuildAsync(
                    repositoryPath,
                    testedSha,
                    initialTree,
                    cancellationToken)
                .ConfigureAwait(false);
            return identity;
        }
        finally
        {
            TryDeleteDirectory(isolatedHome);
        }
    }

    internal async Task ValidateCheckoutCredentialsAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var extraHeaders = await RunGitAsync(
            repositoryPath,
            allowFailure: true,
            cancellationToken,
            "config",
            "--local",
            "--get-regexp",
            "^http\\..*\\.extraheader$").ConfigureAwait(false);
        if (extraHeaders.ExitCode is not 0 and not 1)
        {
            throw new InvalidOperationException(
                $"Unable to inspect local Git HTTP headers; git exited with {extraHeaders.ExitCode}.");
        }
        if (extraHeaders.ExitCode == 0 || !string.IsNullOrWhiteSpace(extraHeaders.StandardOutput))
        {
            throw new InvalidOperationException(BuilderResources.CheckoutPersistedHttpCredentials);
        }

        var remotes = await RunGitAsync(
            repositoryPath,
            allowFailure: false,
            cancellationToken,
            "remote",
            "-v").ConfigureAwait(false);
        if (ContainsCredentialInRemote(remotes.StandardOutput))
        {
            throw new InvalidOperationException(BuilderResources.CheckoutPersistedRemoteCredentials);
        }
    }

    internal static bool ContainsCredentialInRemote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        foreach (var line in value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Contains("x-access-token", StringComparison.OrdinalIgnoreCase)
                || line.Contains("oauth2:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var schemeIndex = line.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
            var authenticatedHostIndex = line.IndexOf("@github.com", StringComparison.OrdinalIgnoreCase);
            if (schemeIndex >= 0 && authenticatedHostIndex > schemeIndex)
            {
                return true;
            }
        }

        return false;
    }

    private async Task BuildAndTestPackageAsync(
        string repositoryPath,
        GitHubActionsBuildIdentity identity,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken)
    {
        var builderProject = Path.Join(repositoryPath, "eng", "Builder", "Builder.csproj");
        var nupkgDirectory = Path.GetDirectoryName(identity.PackagePath)!;
        if (Directory.Exists(nupkgDirectory))
        {
            Directory.Delete(nupkgDirectory, recursive: true);
        }

        await RunRequiredAsync(
            "restore Builder",
            _dotnetPath,
            repositoryPath,
            environment,
            RestoreTimeout,
            cancellationToken,
            "restore",
            builderProject).ConfigureAwait(false);
        await RunRequiredAsync(
            "build Builder",
            _dotnetPath,
            repositoryPath,
            environment,
            BuilderBuildTimeout,
            cancellationToken,
            "build",
            builderProject,
            "--no-restore").ConfigureAwait(false);
        await RunRequiredAsync(
            "build and package WPF",
            _dotnetPath,
            repositoryPath,
            environment,
            FullBuildTimeout,
            cancellationToken,
            "run",
            "--project",
            builderProject,
            "--no-build",
            "--",
            "--version",
            identity.PackageVersion).ConfigureAwait(false);
        RequireNonEmptyFile(identity.PackagePath);
        RequireNonEmptyFile(identity.SymbolPackagePath);
        await RunRequiredAsync(
            "test generated package",
            _dotnetPath,
            repositoryPath,
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
            identity.PackagePath).ConfigureAwait(false);
        RequireNonEmptyFile(identity.PackagePath);
        RequireNonEmptyFile(identity.SymbolPackagePath);
    }

    private Task RebuildSolutionAsync(
        string repositoryPath,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken) =>
        RunRequiredAsync(
            "restore and rebuild solution",
            _msBuildPath,
            repositoryPath,
            environment,
            FullBuildTimeout,
            cancellationToken,
            Path.Join(repositoryPath, "Microsoft.Dotnet.Wpf.slnx"),
            "-restore",
            "/t:Rebuild",
            "/p:Configuration=Debug",
            "/p:Platform=x64",
            "/m:1",
            "/nr:false",
            "/v:minimal");

    private async Task<GitObjectId> ReadTestedShaAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            repositoryPath,
            allowFailure: false,
            cancellationToken,
            "rev-parse",
            "--verify",
            "HEAD^{commit}").ConfigureAwait(false);
        return GitObjectId.Parse(result.StandardOutput.Trim());
    }

    private async Task<GitObjectId> ReadTreeShaAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            repositoryPath,
            allowFailure: false,
            cancellationToken,
            "rev-parse",
            "--verify",
            "HEAD^{tree}").ConfigureAwait(false);
        return GitObjectId.Parse(result.StandardOutput.Trim());
    }

    private async Task<IReadOnlyList<string>> ReadTrackedChangesAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            repositoryPath,
            allowFailure: false,
            cancellationToken,
            "status",
            "--porcelain=v1",
            "--untracked-files=no").ConfigureAwait(false);
        return result.StandardOutput.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private async Task ValidateGitStateAfterBuildAsync(
        string repositoryPath,
        GitObjectId initialCommit,
        GitObjectId initialTree,
        CancellationToken cancellationToken)
    {
        var finalCommit = await ReadTestedShaAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        var finalTree = await ReadTreeShaAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        var finalChanges = await ReadTrackedChangesAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        if (finalCommit != initialCommit || finalTree != initialTree || finalChanges.Count > 0)
        {
            throw new InvalidOperationException(
                $"The tested build modified Git state. HEAD: {initialCommit} -> {finalCommit}; " +
                $"tree: {initialTree} -> {finalTree}; tracked changes: {string.Join(", ", finalChanges)}");
        }
    }

    private async Task ValidateTestedIdentityAsync(
        string repositoryPath,
        GitHubActionsBuildMetadata metadata,
        GitObjectId testedSha,
        CancellationToken cancellationToken)
    {
        if (!metadata.IsPullRequest)
        {
            ValidateTestedIdentity(metadata, testedSha, []);
            return;
        }

        var result = await RunGitAsync(
            repositoryPath,
            allowFailure: false,
            cancellationToken,
            "rev-list",
            "--parents",
            "-n",
            "1",
            testedSha.ToString()).ConfigureAwait(false);
        var identities = result.StandardOutput.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsedCommit = identities.Length > 0 ? GitObjectId.Parse(identities[0]) : default;
        var parents = identities.Skip(1).Select(GitObjectId.Parse).ToArray();
        if (parsedCommit != testedSha)
        {
            throw new InvalidOperationException(BuilderResources.TestedMergeDoesNotMatchPullRequestEvent);
        }

        ValidateTestedIdentity(metadata, testedSha, parents);
    }

    internal static void ValidateTestedIdentity(
        GitHubActionsBuildMetadata metadata,
        GitObjectId testedSha,
        IReadOnlyList<GitObjectId> parents)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(parents);
        if (!metadata.IsPullRequest)
        {
            if (testedSha != metadata.TrustedSha)
            {
                throw new InvalidOperationException(BuilderResources.TestedCommitDoesNotMatchEventSha);
            }

            return;
        }

        if (parents.Count != 2
            || parents[0] != metadata.TrustedSha
            || parents[1] != metadata.SourceHeadSha)
        {
            throw new InvalidOperationException(BuilderResources.TestedMergeDoesNotMatchPullRequestEvent);
        }
    }

    private async Task<ProcessResult> RunGitAsync(
        string repositoryPath,
        bool allowFailure,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var isolatedHome = Path.Join(
            Path.GetTempPath(),
            "WpfReorganize.Builder",
            "git-inspection",
            Guid.NewGuid().ToString("N"));
        try
        {
            var environment = ProcessEnvironment.CreateUntrustedBuildEnvironment(isolatedHome);
            var result = await ProcessRunner.RunAsync(
                new ProcessRunOptions(
                    _gitPath,
                    repositoryPath,
                    ["-c", "core.fsmonitor=false", .. arguments])
                {
                    Timeout = GitTimeout,
                    InheritEnvironment = false,
                    EnvironmentVariables = environment,
                },
                cancellationToken).ConfigureAwait(false);
            if (!allowFailure && result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Git command failed with exit code {result.ExitCode}; credential output is omitted.");
            }

            return result;
        }
        finally
        {
            TryDeleteDirectory(isolatedHome);
        }
    }

    private static async Task RunRequiredAsync(
        string description,
        string fileName,
        string workingDirectory,
        IReadOnlyDictionary<string, string?> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        Log.Step(description);
        var result = await ProcessRunner.RunAsync(
            new ProcessRunOptions(fileName, workingDirectory, arguments)
            {
                Timeout = timeout,
                InheritEnvironment = false,
                EnvironmentVariables = environment,
            },
            cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                Console.Error.Write(result.StandardOutput);
            }
            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                Console.Error.Write(result.StandardError);
            }

            throw new InvalidOperationException(
                $"GitHub Actions step '{description}' failed with exit code {result.ExitCode}.");
        }

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            Console.Write(result.StandardOutput);
        }
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            Console.Error.Write(result.StandardError);
        }
    }

    private static void LogIdentity(
        GitHubActionsBuildMetadata metadata,
        GitHubActionsBuildIdentity identity)
    {
        Log.Info($"Tested merge/commit SHA: {identity.TestedSha}");
        if (metadata.IsPullRequest)
        {
            Log.Info($"Source PR head SHA: {metadata.SourceHeadSha}");
            Log.Info($"Trusted workflow/base SHA: {metadata.TrustedSha}");
        }
        Log.Info($"Package version: {identity.PackageVersion}");
    }

    private static void RequireDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Required directory was not found: {path}");
        }
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required file was not found: {path}", path);
        }
    }

    private static void RequireNonEmptyFile(string path)
    {
        RequireFile(path);
        if (new FileInfo(path).Length == 0)
        {
            throw new InvalidOperationException($"Required file is empty: {path}");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (UnauthorizedAccessException)
        {
            Log.Warn($"Unable to delete isolated CI directory: {path}");
        }
        catch (IOException)
        {
            Log.Warn($"Unable to delete isolated CI directory: {path}");
        }
    }
}

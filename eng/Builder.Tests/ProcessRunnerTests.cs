using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_PreservesArgumentsWithoutShellParsing()
    {
        var arguments = new[] { "value with spaces", "quoted\"value", "-starts-with-dash" };
        var processArguments = new[] { "echo" }.Concat(arguments).ToArray();
        var result = await ProcessRunner.RunAsync(
            new ProcessRunOptions(
                HelperPath,
                AppContext.BaseDirectory,
                processArguments));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(arguments, SplitLines(result.StandardOutput));
    }

    [Fact]
    public async Task RunAsync_CancellationTerminatesProcessTree()
    {
        var markerPath = Path.Join(Path.GetTempPath(), $"builder-process-{Guid.NewGuid():N}.txt");
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                ProcessRunner.RunAsync(
                    new ProcessRunOptions(
                        HelperPath,
                        AppContext.BaseDirectory,
                        "parent",
                        markerPath),
                    cancellationTokenSource.Token));

            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            File.Delete(markerPath);
        }
    }

    [Fact]
    public async Task RunAsync_TimeoutTerminatesProcessTree()
    {
        var markerPath = Path.Join(Path.GetTempPath(), $"builder-process-{Guid.NewGuid():N}.txt");
        try
        {
            var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
                ProcessRunner.RunAsync(
                    new ProcessRunOptions(
                        HelperPath,
                        AppContext.BaseDirectory,
                        "parent",
                        markerPath)
                    {
                        Timeout = TimeSpan.FromMilliseconds(500),
                    }));

            Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            File.Delete(markerPath);
        }
    }

    [Fact]
    public void GitPushEnvironment_AllowsCredentialManagerAndRemovesApiTokens()
    {
        var environment = ProcessEnvironment.CreateGitPushEnvironment();

        Assert.Null(environment["GITHUB_TOKEN"]);
        Assert.Null(environment["GIT_TERMINAL_PROMPT"]);
        Assert.Null(environment["GCM_INTERACTIVE"]);
    }

    [Fact]
    public void UntrustedEnvironment_RemovesTokensAndUsesIsolatedDirectories()
    {
        var home = Path.Join(Path.GetTempPath(), $"builder-home-{Guid.NewGuid():N}");
        const string tokenName = "GITHUB_TOKEN";
        var original = Environment.GetEnvironmentVariable(tokenName);
        try
        {
            Environment.SetEnvironmentVariable(tokenName, "secret-value");
            var environment = ProcessEnvironment.CreateUntrustedBuildEnvironment(home);

            Assert.DoesNotContain(tokenName, environment.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(home, environment["HOME"]);
            Assert.StartsWith(home, environment["NUGET_PACKAGES"], StringComparison.OrdinalIgnoreCase);
            Assert.False(File.ReadAllText(Path.Join(home, ".gitconfig")).Contains("secret-value", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable(tokenName, original);
            if (Directory.Exists(home))
            {
                Directory.Delete(home, recursive: true);
            }
        }
    }

    private static string HelperPath => Path.Join(AppContext.BaseDirectory, "Builder.ProcessTestHelper.exe");

    private static string[] SplitLines(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

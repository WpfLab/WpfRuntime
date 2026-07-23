namespace WpfReorganize.Builder.Tests;

public sealed class WorkflowContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void BuildWorkflow_UsesTrustedReadOnlyPullRequestTargetContract()
    {
        var workflow = ReadWorkflow("build.yml");

        Assert.Contains("  pull_request_target:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("  pull_request:\n", NormalizeNewLines(workflow), StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", NormalizeNewLines(workflow), StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(workflow, "persist-credentials: false"));
        Assert.Equal(2, CountOccurrences(workflow, "refs/pull/{0}/merge"));
        Assert.Contains("if-no-files-found: error", workflow, StringComparison.Ordinal);
        Assert.Contains("github.run_attempt", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildWorkflow_PinsEveryThirdPartyActionToAFullCommit()
    {
        var workflow = ReadWorkflow("build.yml");
        var actionLines = NormalizeNewLines(workflow)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("uses:", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(actionLines);
        Assert.All(actionLines, line =>
        {
            var separator = line.LastIndexOf('@');
            Assert.True(separator > 0, line);
            var revision = line[(separator + 1)..];
            Assert.Equal(40, revision.Length);
            Assert.All(revision, character => Assert.True(Uri.IsHexDigit(character), line));
        });
    }

    [Fact]
    public void CommentWorkflow_UsesMetadataOnlyLeastPrivilegeContract()
    {
        var workflow = ReadWorkflow("comment-pr-build-artifacts.yml");
        var normalized = NormalizeNewLines(workflow);

        Assert.Contains("  workflow_run:", workflow, StringComparison.Ordinal);
        Assert.Contains("  actions: read", workflow, StringComparison.Ordinal);
        Assert.Contains("  pull-requests: write", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("contents: write", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("issues: write", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/checkout", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("download-artifact", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("downloadArtifact", workflow, StringComparison.Ordinal);
        Assert.Contains("listWorkflowRunArtifacts", workflow, StringComparison.Ordinal);
        Assert.Contains("artifact.expired === false", workflow, StringComparison.Ordinal);
        Assert.Contains("github-actions[bot]", workflow, StringComparison.Ordinal);
        Assert.Contains("findNewerAssociatedRun", workflow, StringComparison.Ordinal);
        Assert.Contains("workflow_run.pull_requests", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void CommentWorkflow_PinsGitHubScriptToAFullCommit()
    {
        var workflow = ReadWorkflow("comment-pr-build-artifacts.yml");
        const string prefix = "uses: actions/github-script@";
        var line = NormalizeNewLines(workflow)
            .Split('\n')
            .Select(value => value.Trim())
            .Single(value => value.StartsWith(prefix, StringComparison.Ordinal));
        var revision = line[prefix.Length..];

        Assert.Equal(40, revision.Length);
        Assert.All(revision, character => Assert.True(Uri.IsHexDigit(character)));
    }

    private static string ReadWorkflow(string fileName) =>
        File.ReadAllText(Path.Join(RepositoryRoot, ".github", "workflows", fileName));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "Microsoft.Dotnet.Wpf.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static string NormalizeNewLines(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }
}

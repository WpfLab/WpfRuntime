namespace WpfReorganize.Builder.Tests;

public sealed class WorkflowContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void BuildWorkflow_UsesTrustedReadOnlyPullRequestTargetContract()
    {
        var workflow = ReadWorkflow("build.yml");
        var normalized = NormalizeNewLines(workflow);

        Assert.Contains("  pull_request_target:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("  pull_request:\n", normalized, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("permissions:\n  contents: read\n  packages: write", normalized, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(normalized, "    permissions:\n      contents: read"));
        Assert.Equal(4, CountOccurrences(workflow, "persist-credentials: false"));
        Assert.Equal(2, CountOccurrences(workflow, "refs/pull/{0}/merge"));
        Assert.Equal(2, CountOccurrences(normalized, "        path: trusted\n        fetch-depth: 1"));
        Assert.Equal(2, CountOccurrences(normalized, "        path: tested\n        fetch-depth: 0"));
        Assert.Equal(2, CountOccurrences(workflow, "Builder.dll ci-build"));
        Assert.DoesNotContain("github.event.pull_request.head.sha", workflow, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(workflow, "tested/eng/Builder/bin/nupkg/*."));
        Assert.Contains("path: tested/eng/Builder/bin/nupkg/*.nupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("path: tested/eng/Builder/bin/nupkg/*.snupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("path: tested/eng/Builder/bin/nupkg/*.symbols.zip", workflow, StringComparison.Ordinal);
        Assert.Contains("name: ${{ steps.build-package.outputs.artifact-name }}.nupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("name: ${{ steps.build-package.outputs.artifact-name }}.snupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("name: ${{ steps.build-package.outputs.artifact-name }}.symbols.zip", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("steps.build-package.outputs.symbol-package-path", workflow, StringComparison.Ordinal);
        Assert.Contains("name: Push generated package to NuGet registries", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("if: github.event_name != 'pull_request_target'", workflow, StringComparison.Ordinal);
        Assert.Contains("  publish-package:\n    needs: [build-solution, build-package]", normalized, StringComparison.Ordinal);
        Assert.Contains("    permissions:\n      actions: read\n      contents: read\n      packages: write", normalized, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(workflow, "uses: actions/download-artifact@v7"));
        Assert.Contains("name: ${{ needs.build-package.outputs.artifact-name }}.nupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("name: ${{ needs.build-package.outputs.artifact-name }}.snupkg", workflow, StringComparison.Ordinal);
        Assert.Contains("$packagePath = \"nupkg/WpfLab.WpfRuntime.${{ needs.build-package.outputs.version }}.nupkg\"", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet nuget push $packagePath -s https://api.nuget.org/v3/index.json -k \"${{ secrets.NugetKey }}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet nuget push $packagePath -s \"https://nuget.pkg.github.com/${{ github.repository_owner }}\" --api-key \"${{ secrets.GITHUB_TOKEN }}\"", workflow, StringComparison.Ordinal);
        var publishJobIndex = normalized.IndexOf("\n  publish-package:", StringComparison.Ordinal);
        Assert.True(publishJobIndex > 0);
        Assert.DoesNotContain("secrets.NugetKey", normalized[..publishJobIndex], StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.GITHUB_TOKEN", normalized[..publishJobIndex], StringComparison.Ordinal);
        Assert.Contains("if-no-files-found: error", workflow, StringComparison.Ordinal);
        Assert.Contains("github.run_attempt", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryAttributes_PreserveCommittedLineEndings()
    {
        var attributes = File.ReadAllText(Path.Join(RepositoryRoot, ".gitattributes"));

        Assert.Equal("* -text\n", NormalizeNewLines(attributes));
    }

    [Fact]
    public void BuilderProject_NormalizesNuGetPackageRootBeforeWritingPackagePaths()
    {
        var project = File.ReadAllText(Path.Join(RepositoryRoot, "eng", "Builder", "Builder.csproj"));

        Assert.Contains("$([MSBuild]::EnsureTrailingSlash('$(NuGetPackageRoot)'))", project, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildWorkflow_UsesMajorVersionTagsForEveryThirdPartyAction()
    {
        var workflow = ReadWorkflow("build.yml");

        AssertActionsUseMajorVersionTags(workflow);
    }

    [Fact]
    public void CommentWorkflow_UsesMetadataOnlyLeastPrivilegeContract()
    {
        var workflow = ReadWorkflow("comment-pr-build-artifacts.yml");
        var normalized = NormalizeNewLines(workflow);

        Assert.Contains("  workflow_run:", workflow, StringComparison.Ordinal);
        Assert.Contains("  actions: read", workflow, StringComparison.Ordinal);
        Assert.Contains("  contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("  pull-requests: write", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("contents: write", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("issues: write", workflow, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(workflow, "uses: actions/checkout@"));
        Assert.Contains("        ref: ${{ github.sha }}", workflow, StringComparison.Ordinal);
        Assert.Contains("        path: trusted\n        fetch-depth: 1", normalized, StringComparison.Ordinal);
        Assert.Contains("        persist-credentials: false", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("download-artifact", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("downloadArtifact", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("actions/github-script", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("script:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("run: |", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("refs/pull/", workflow, StringComparison.Ordinal);
        Assert.Contains("Builder.dll comment-pr-artifacts", workflow, StringComparison.Ordinal);
        Assert.Contains("GITHUB_TOKEN: ${{ github.token }}", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void CommentWorkflow_UsesMajorVersionTagsForEveryThirdPartyAction()
    {
        var workflow = ReadWorkflow("comment-pr-build-artifacts.yml");

        AssertActionsUseMajorVersionTags(workflow);
    }

    private static void AssertActionsUseMajorVersionTags(string workflow)
    {
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
            var majorVersion = revision.StartsWith('v') ? revision[1..] : revision;
            Assert.NotEmpty(majorVersion);
            Assert.All(majorVersion, character => Assert.True(char.IsAsciiDigit(character), line));
        });
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

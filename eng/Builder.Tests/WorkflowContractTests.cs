namespace WpfReorganize.Builder.Tests;

public sealed class WorkflowContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void BuildWorkflow_UsesTrustedReadOnlyPullRequestTargetContract()
    {
        var workflow = ReadWorkflow("build.yml");
        Assert.Contains("name: Build WPF NuGet Package", workflow, StringComparison.Ordinal);
        Assert.Contains("  push:", workflow, StringComparison.Ordinal);
        Assert.Contains("    tags: [ '*' ]", workflow, StringComparison.Ordinal);
        Assert.Contains("  workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("  pull_request_target:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("  pull_request:\n", workflow, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("packages: write", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("id-token: write", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", workflow, StringComparison.Ordinal);
        Assert.Equal(4, CountOccurrences(workflow, "persist-credentials: false"));
        Assert.Equal(2, CountOccurrences(workflow, "refs/pull/{0}/merge"));
        Assert.Equal(2, CountOccurrences(workflow, "Builder.dll ci-build"));
        Assert.Contains("if: github.event_name != 'push' || github.ref != 'refs/heads/main'", workflow, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nupkg")]
    [InlineData("snupkg")]
    [InlineData("symbols.zip")]
    public void BuildWorkflow_PreservesBuilderArtifactContract(string extension)
    {
        var workflow = ReadWorkflow("build.yml");
        Assert.Contains($"name: ${{{{ steps.build-package.outputs.artifact-name }}}}.{extension}", workflow, StringComparison.Ordinal);
        Assert.Contains($"path: tested/eng/Builder/bin/nupkg/*.{extension}", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishWorkflow_DelegatesPublishingToTrustedBuilder()
    {
        var workflow = ReadWorkflow("publish-nuget.yml");
        Assert.Contains("workflows: [Build WPF NuGet Package]", workflow, StringComparison.Ordinal);
        Assert.Contains("types: [completed]", workflow, StringComparison.Ordinal);
        Assert.Contains("if: github.event.workflow_run.conclusion == 'success'", workflow, StringComparison.Ordinal);
        Assert.Contains("ref: ${{ github.sha }}", workflow, StringComparison.Ordinal);
        Assert.Contains("persist-credentials: false", workflow, StringComparison.Ordinal);
        Assert.Contains("Builder.dll publish-packages", workflow, StringComparison.Ordinal);
        Assert.Contains("PUBLISH_REPOSITORY: ${{ github.repository }}", workflow, StringComparison.Ordinal);
        Assert.Contains("PUBLISH_RUN_ID: ${{ github.event.workflow_run.id }}", workflow, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(workflow, "-p:RestoreWpfRuntimePackages=false"));
        Assert.DoesNotContain("validate:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("github-script", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("download-artifact", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("refs/pull/", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("run: |", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishWorkflow_UsesOidcWithoutLongLivedKeys()
    {
        var workflow = ReadWorkflow("publish-nuget.yml");
        Assert.Contains("      packages: write\n      id-token: write", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: NuGet/login@v1", workflow, StringComparison.Ordinal);
        Assert.Contains("user: lindexi", workflow, StringComparison.Ordinal);
        Assert.Contains("NUGET_API_KEY: ${{ steps.nuget-login.outputs.NUGET_API_KEY }}", workflow, StringComparison.Ordinal);
        Assert.Contains("GITHUB_TOKEN: ${{ github.token }}", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", workflow, StringComparison.Ordinal);
        Assert.True(workflow.IndexOf("Build trusted Builder", StringComparison.Ordinal) < workflow.IndexOf("NuGet login (OIDC)", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryAttributes_PreserveCommittedLineEndings()
    {
        Assert.Equal("* -text\n", NormalizeNewLines(File.ReadAllText(Path.Join(RepositoryRoot, ".gitattributes"))));
    }

    [Fact]
    public void BuilderProject_NormalizesNuGetPackageRootBeforeWritingPackagePaths()
    {
        var project = File.ReadAllText(Path.Join(RepositoryRoot, "eng", "Builder", "Builder.csproj"));
        Assert.Contains("$([MSBuild]::EnsureTrailingSlash('$(NuGetPackageRoot)'))", project, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("build.yml")]
    [InlineData("publish-nuget.yml")]
    [InlineData("comment-pr-build-artifacts.yml")]
    public void Workflow_UsesMajorVersionTagsForEveryThirdPartyAction(string fileName)
    {
        var actions = ReadWorkflow(fileName).Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith("uses:", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(actions);
        Assert.All(actions, line =>
        {
            var revision = line[(line.LastIndexOf('@') + 1)..];
            Assert.StartsWith("v", revision, StringComparison.Ordinal);
            Assert.NotEmpty(revision[1..]);
            Assert.All(revision[1..], character => Assert.True(char.IsAsciiDigit(character), line));
        });
    }

    [Fact]
    public void CommentWorkflow_UsesMetadataOnlyLeastPrivilegeContract()
    {
        var workflow = ReadWorkflow("comment-pr-build-artifacts.yml");
        Assert.Contains("workflows: [Build WPF NuGet Package]", workflow, StringComparison.Ordinal);
        Assert.Contains("  pull-requests: write", workflow, StringComparison.Ordinal);
        Assert.Contains("ref: ${{ github.sha }}", workflow, StringComparison.Ordinal);
        Assert.Contains("persist-credentials: false", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("download-artifact", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github-script", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("refs/pull/", workflow, StringComparison.Ordinal);
        Assert.Contains("Builder.dll comment-pr-artifacts", workflow, StringComparison.Ordinal);
    }

    private static string ReadWorkflow(string fileName) => NormalizeNewLines(File.ReadAllText(Path.Join(RepositoryRoot, ".github", "workflows", fileName)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "Microsoft.Dotnet.Wpf.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static string NormalizeNewLines(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static int CountOccurrences(string value, string search) => value.Split(search, StringSplitOptions.None).Length - 1;
}

using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class GitHubActionsBuildTests
{
    private const string SourceHeadSha = "1111111111111111111111111111111111111111";
    private const string TrustedSha = "2222222222222222222222222222222222222222";
    private const string TestedSha = "3333333333333333333333333333333333333333";

    [Fact]
    public void PullRequestEvent_CreatesTraceablePackageIdentity()
    {
        var eventPath = CreateTemporaryFile(
            $$"""
            {
              "pull_request": {
                "number": 11781,
                "head": { "sha": "{{SourceHeadSha}}" }
              }
            }
            """);
        var repositoryPath = Path.Join(Path.GetTempPath(), $"builder-identity-{Guid.NewGuid():N}");
        try
        {
            var metadata = GitHubActionsBuildMetadata.Read(
                eventPath,
                "pull_request_target",
                TrustedSha);
            var identity = GitHubActionsBuildIdentity.Create(
                metadata,
                repositoryPath,
                TestedSha,
                runId: 42,
                runAttempt: 3);

            Assert.True(metadata.IsPullRequest);
            Assert.Equal(11781, metadata.PullRequestNumber);
            Assert.Equal(SourceHeadSha, metadata.SourceHeadSha.ToString());
            Assert.Equal("0.0.0-pr.11781.sha333333333333", identity.PackageVersion);
            Assert.Equal(
                Path.GetFullPath(Path.Join(
                    repositoryPath,
                    "eng",
                    "Builder",
                    "bin",
                    "nupkg",
                    "DotNetCampus.WpfLib.0.0.0-pr.11781.sha333333333333.nupkg")),
                identity.PackagePath);
            Assert.Equal(
                Path.GetFullPath(Path.Join(
                    repositoryPath,
                    "eng",
                    "Builder",
                    "bin",
                    "nupkg",
                    "DotNetCampus.WpfLib.0.0.0-pr.11781.sha333333333333.snupkg")),
                identity.SymbolPackagePath);
            Assert.Equal(
                $"DotNetCampus.WpfLib-nupkg-pr-11781-sha-{TestedSha}-run-42-attempt-3",
                identity.ArtifactName);
        }
        finally
        {
            File.Delete(eventPath);
        }
    }

    [Fact]
    public void PushEvent_UsesCiIdentityAndRejectsPullRequestMetadata()
    {
        var metadata = GitHubActionsBuildMetadata.Create("push", TrustedSha, null, null);
        var identity = GitHubActionsBuildIdentity.Create(
            metadata,
            Path.GetTempPath(),
            TrustedSha,
            runId: 7,
            runAttempt: 1);

        Assert.False(metadata.IsPullRequest);
        Assert.Equal("0.0.0-ci.sha222222222222", identity.PackageVersion);
        Assert.Equal(
            $"DotNetCampus.WpfLib-nupkg-event-push-sha-{TrustedSha}-run-7-attempt-1",
            identity.ArtifactName);
        Assert.Throws<ArgumentException>(() =>
            GitHubActionsBuildMetadata.Create("push", TrustedSha, 1, SourceHeadSha));
    }

    [Fact]
    public void PullRequestEvent_RequiresNumberAndFullHeadSha()
    {
        Assert.Throws<ArgumentException>(() =>
            GitHubActionsBuildMetadata.Create("pull_request_target", TrustedSha, null, SourceHeadSha));
        Assert.Throws<ArgumentException>(() =>
            GitHubActionsBuildMetadata.Create("pull_request_target", TrustedSha, 1, "short"));
        Assert.Throws<ArgumentException>(() =>
            GitHubActionsBuildMetadata.Create("schedule", TrustedSha, null, null));
    }

    [Theory]
    [InlineData("origin\thttps://github.com/owner/repository.git (fetch)", false)]
    [InlineData("origin\tgit@github.com:owner/repository.git (fetch)", false)]
    [InlineData("origin\thttps://x-access-token:secret@github.com/owner/repository.git (fetch)", true)]
    [InlineData("origin\thttps://oauth2:secret@github.com/owner/repository.git (fetch)", true)]
    [InlineData("origin\thttps://user@github.com/owner/repository.git (fetch)", true)]
    public void RemoteCredentialDetection_RecognizesEmbeddedGitHubCredentials(
        string remoteOutput,
        bool expected)
    {
        Assert.Equal(expected, GitHubActionsBuildService.ContainsCredentialInRemote(remoteOutput));
    }

    [Fact]
    public void GitHubOutput_WritesOnlyValidatedSingleLineValues()
    {
        var outputPath = Path.Join(Path.GetTempPath(), $"builder-output-{Guid.NewGuid():N}.txt");
        try
        {
            GitHubActionsOutput.Write(
                outputPath,
                new Dictionary<string, string>
                {
                    ["tested-sha"] = TestedSha,
                    ["artifact_name"] = "safe-value",
                });

            var output = File.ReadAllText(outputPath);
            Assert.Contains($"tested-sha={TestedSha}", output, StringComparison.Ordinal);
            Assert.Contains("artifact_name=safe-value", output, StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() =>
                GitHubActionsOutput.Write(
                    outputPath,
                    new Dictionary<string, string> { ["unsafe"] = "first\nsecond" }));
            Assert.Throws<ArgumentException>(() =>
                GitHubActionsOutput.Write(
                    outputPath,
                    new Dictionary<string, string> { ["unsafe=name"] = "value" }));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void TestedIdentity_RequiresExactEventCommitOrMergeParents()
    {
        var pullRequestMetadata = GitHubActionsBuildMetadata.Create(
            "pull_request_target",
            TrustedSha,
            11781,
            SourceHeadSha);
        GitHubActionsBuildService.ValidateTestedIdentity(
            pullRequestMetadata,
            GitObjectId.Parse(TestedSha),
            [GitObjectId.Parse(TrustedSha), GitObjectId.Parse(SourceHeadSha)]);

        Assert.Throws<InvalidOperationException>(() =>
            GitHubActionsBuildService.ValidateTestedIdentity(
                pullRequestMetadata,
                GitObjectId.Parse(TestedSha),
                [GitObjectId.Parse(SourceHeadSha), GitObjectId.Parse(TrustedSha)]));
        Assert.Throws<InvalidOperationException>(() =>
            GitHubActionsBuildService.ValidateTestedIdentity(
                pullRequestMetadata,
                GitObjectId.Parse(TestedSha),
                [GitObjectId.Parse(TrustedSha), GitObjectId.Parse(SourceHeadSha), GitObjectId.Parse(TestedSha)]));

        var pushMetadata = GitHubActionsBuildMetadata.Create("push", TrustedSha, null, null);
        GitHubActionsBuildService.ValidateTestedIdentity(
            pushMetadata,
            GitObjectId.Parse(TrustedSha),
            []);
        Assert.Throws<InvalidOperationException>(() =>
            GitHubActionsBuildService.ValidateTestedIdentity(
                pushMetadata,
                GitObjectId.Parse(TestedSha),
                []));
    }

    private static string CreateTemporaryFile(string content)
    {
        var path = Path.Join(Path.GetTempPath(), $"builder-event-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        return path;
    }
}

using Octokit;
using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class GitHubArtifactCommentTests
{
    private const string HeadSha = "1111111111111111111111111111111111111111";
    private const string TestedSha = "2222222222222222222222222222222222222222";

    [Fact]
    public void WorkflowRunEvent_ReadsTrustedRepositoryAndAssociation()
    {
        var eventPath = CreateWorkflowRunEvent();
        try
        {
            var workflowEvent = GitHubWorkflowRunEvent.Read(eventPath, "owner/repository");

            Assert.Equal("owner/repository", workflowEvent.Repository.FullName);
            Assert.Equal(42, workflowEvent.RunId);
            Assert.Equal(9, workflowEvent.WorkflowId);
            Assert.Equal(2, workflowEvent.RunAttempt);
            Assert.Equal("pull_request_target", workflowEvent.EventName);
            var association = Assert.Single(workflowEvent.PullRequests);
            Assert.Equal(11781, association.Number);
            Assert.Equal(HeadSha, association.HeadSha.ToString());
            Assert.Equal("main", association.BaseReference);
        }
        finally
        {
            File.Delete(eventPath);
        }
    }

    [Fact]
    public void WorkflowRunEvent_RejectsRepositoryMismatchAndInvalidAssociation()
    {
        var eventPath = CreateWorkflowRunEvent();
        try
        {
            Assert.Throws<InvalidDataException>(() =>
                GitHubWorkflowRunEvent.Read(eventPath, "other/repository"));
        }
        finally
        {
            File.Delete(eventPath);
        }

        var invalidEventPath = CreateWorkflowRunEvent(headSha: "short");
        try
        {
            Assert.Throws<InvalidDataException>(() =>
                GitHubWorkflowRunEvent.Read(invalidEventPath, "owner/repository"));
        }
        finally
        {
            File.Delete(invalidEventPath);
        }
    }

    [Fact]
    public void ArtifactFilter_RequiresExactIdentityAndSortsById()
    {
        var artifacts = new[]
        {
            CreateArtifact(2, $"DotNetCampus.WpfLib-nupkg-pr-11781-sha-{TestedSha}-run-42-attempt-2"),
            CreateArtifact(1, $"DotNetCampus.WpfLib-nupkg-pr-11781-sha-{TestedSha}-run-42-attempt-2"),
            CreateArtifact(3, $"DotNetCampus.WpfLib-nupkg-pr-11781-sha-{HeadSha}-run-99-attempt-2"),
            CreateArtifact(4, $"DotNetCampus.WpfLib-nupkg-pr-11781-sha-{HeadSha}-run-42-attempt-2", expired: true),
            CreateArtifact(5, $"DotNetCampus.WpfLib-nupkg-pr-11781-sha-{HeadSha}-run-42-attempt-2", size: 0),
        };

        var filtered = GitHubArtifactCommentFormatter.FilterArtifacts(artifacts, 11781, 42, 2);

        Assert.Equal([1L, 2L], filtered.Select(artifact => artifact.Id));
        Assert.All(filtered, artifact => Assert.Equal(TestedSha, artifact.TestedSha.ToString()));
    }

    [Fact]
    public void CommentFormatter_CreatesSuccessfulIdempotentCommentAndEscapesArtifactName()
    {
        var repository = new GitHubRepositoryAddress("owner", "repository");
        var content = GitHubArtifactCommentFormatter.Create(
            repository,
            11781,
            GitObjectId.Parse(HeadSha),
            42,
            2,
            "success",
            [
                new GitHubArtifactCommentItem(
                    7,
                    "package@team[debug]",
                    1536,
                    DateTime.Parse("2025-02-03T04:05:06Z").ToUniversalTime(),
                    GitObjectId.Parse(TestedSha)),
            ]);

        Assert.True(content.HasValidSuccessArtifacts);
        Assert.Equal(TestedSha, content.TestedSha.ToString());
        Assert.Contains("<!-- wpf-nuget-artifacts workflow=build pr=11781 -->", content.Body, StringComparison.Ordinal);
        Assert.Contains("<!-- wpf-nuget-artifacts-run id=42 attempt=2 -->", content.Body, StringComparison.Ordinal);
        Assert.Contains("## WPF NuGet Build", content.Body, StringComparison.Ordinal);
        Assert.Contains("- Result: Succeeded", content.Body, StringComparison.Ordinal);
        Assert.Contains("package@\u200bteam\\[debug\\]", content.Body, StringComparison.Ordinal);
        Assert.Contains("1.5 KiB", content.Body, StringComparison.Ordinal);
        Assert.Contains("actions/runs/42/artifacts/7", content.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("下载需要 GitHub 登录和仓库读取权限", content.Body, StringComparison.Ordinal);
        Assert.True(GitHubArtifactCommentFormatter.TryReadRunIdentity(content.Body, out var runId, out var attempt));
        Assert.Equal(42, runId);
        Assert.Equal(2, attempt);
    }

    [Fact]
    public void CommentFormatter_RejectsAmbiguousArtifactShaAndOrdersRunAttempts()
    {
        var content = GitHubArtifactCommentFormatter.Create(
            new GitHubRepositoryAddress("owner", "repository"),
            11781,
            GitObjectId.Parse(HeadSha),
            42,
            2,
            "success",
            [
                new GitHubArtifactCommentItem(1, "one", 1, DateTime.UtcNow, GitObjectId.Parse(TestedSha)),
                new GitHubArtifactCommentItem(2, "two", 1, DateTime.UtcNow, GitObjectId.Parse(HeadSha)),
            ]);

        Assert.False(content.HasValidSuccessArtifacts);
        Assert.Null(content.TestedSha);
        Assert.Contains("no unique valid nupkg artifact was found", content.Body, StringComparison.Ordinal);
        Assert.True(GitHubArtifactCommentFormatter.CompareRunIdentity(42, 3, 42, 2) > 0);
        Assert.True(GitHubArtifactCommentFormatter.CompareRunIdentity(43, 1, 42, 99) > 0);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1024, "1.0 KiB")]
    [InlineData(1048576, "1.0 MiB")]
    [InlineData(-1, "unknown")]
    public void FormatBytes_UsesBinaryUnits(long value, string expected)
    {
        Assert.Equal(expected, GitHubArtifactCommentFormatter.FormatBytes(value));
    }

    private static Artifact CreateArtifact(long id, string name, bool expired = false, int size = 10) =>
        new(
            id,
            "node",
            name,
            size,
            "https://api.github.com/artifacts/1",
            "https://api.github.com/artifacts/1/zip",
            expired,
            DateTime.UtcNow,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            new ArtifactWorkflowRun());

    private static string CreateWorkflowRunEvent(string headSha = HeadSha)
    {
        var path = Path.Join(Path.GetTempPath(), $"builder-workflow-run-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            path,
            $$"""
            {
              "repository": { "full_name": "owner/repository" },
              "workflow_run": {
                "id": 42,
                "workflow_id": 9,
                "run_attempt": 2,
                "event": "pull_request_target",
                "conclusion": "success",
                "status": "completed",
                "created_at": "2025-02-03T04:05:06Z",
                "repository": { "full_name": "owner/repository" },
                "pull_requests": [
                  {
                    "number": 11781,
                    "head": { "sha": "{{headSha}}" },
                    "base": { "ref": "main" }
                  }
                ]
              }
            }
            """);
        return path;
    }
}

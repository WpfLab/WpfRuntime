namespace WpfReorganize.Builder;

using Octokit;

internal sealed record GitHubArtifactCommentResult(
    bool Updated,
    int? PullRequestNumber,
    long? RunAttempt,
    string Message)
{
    public static GitHubArtifactCommentResult Skip(string reason) => new(false, null, null, reason);

    public static GitHubArtifactCommentResult Success(
        int pullRequestNumber,
        long runAttempt,
        string message) =>
        new(true, pullRequestNumber, runAttempt, message);
}

internal sealed class GitHubArtifactCommentService
{
    private const int PageSize = 100;
    private static readonly HashSet<string> SupportedBases = ["main", "WpfReorganize"];
    private readonly IGitHubClient _client;

    public GitHubArtifactCommentService(string token)
        : this(CreateClient(token))
    {
    }

    internal GitHubArtifactCommentService(IGitHubClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public async Task<GitHubArtifactCommentResult> RunAsync(
        GitHubWorkflowRunEvent workflowEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);
        if (!string.Equals(workflowEvent.EventName, "pull_request_target", StringComparison.Ordinal)
            || !string.Equals(
                workflowEvent.RunRepository.FullName,
                workflowEvent.Repository.FullName,
                StringComparison.OrdinalIgnoreCase))
        {
            return GitHubArtifactCommentResult.Skip(
                "workflow_run did not originate from this repository pull_request_target workflow.");
        }

        if (workflowEvent.PullRequests.Count != 1)
        {
            return GitHubArtifactCommentResult.Skip(
                $"workflow_run.pull_requests contained {workflowEvent.PullRequests.Count} entries; exactly one is required.");
        }

        var repository = workflowEvent.Repository;
        var association = workflowEvent.PullRequests[0];
        cancellationToken.ThrowIfCancellationRequested();
        var currentRun = await _client.Actions.Workflows.Runs
            .Get(repository.Owner, repository.Repository, workflowEvent.RunId)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        var currentRunAttempt = currentRun.RunAttempt <= 0 ? 1 : currentRun.RunAttempt;
        var runAttempt = workflowEvent.RunAttempt ?? currentRunAttempt;
        var runError = ValidateCurrentRun(workflowEvent, currentRun, association, runAttempt);
        if (runError is not null)
        {
            return GitHubArtifactCommentResult.Skip(runError);
        }

        var pullRequest = await ReadPullRequestAsync(repository, association.Number, cancellationToken)
            .ConfigureAwait(false);
        var pullRequestError = ValidatePullRequest(pullRequest, association, currentRun.CreatedAt);
        if (pullRequestError is not null)
        {
            return GitHubArtifactCommentResult.Skip(pullRequestError);
        }

        var artifacts = await ReadArtifactsAsync(repository, workflowEvent.RunId, cancellationToken)
            .ConfigureAwait(false);
        var validArtifacts = GitHubArtifactCommentFormatter.FilterArtifacts(
            artifacts,
            association.Number,
            workflowEvent.RunId,
            runAttempt);

        pullRequest = await ReadPullRequestAsync(repository, association.Number, cancellationToken)
            .ConfigureAwait(false);
        pullRequestError = ValidatePullRequest(pullRequest, association, currentRun.CreatedAt);
        if (pullRequestError is not null)
        {
            return GitHubArtifactCommentResult.Skip(pullRequestError);
        }

        var newerRun = await FindNewerAssociatedRunAsync(
                repository,
                currentRun,
                association.Number,
                runAttempt,
                cancellationToken)
            .ConfigureAwait(false);
        if (newerRun is not null)
        {
            return GitHubArtifactCommentResult.Skip(
                $"A newer associated workflow run exists: {newerRun.Id}.");
        }

        var conclusion = currentRun.Conclusion?.StringValue
            ?? workflowEvent.Conclusion
            ?? "unknown";
        var content = GitHubArtifactCommentFormatter.Create(
            repository,
            association.Number,
            GitObjectId.Parse(pullRequest.Head.Sha),
            workflowEvent.RunId,
            runAttempt,
            conclusion,
            validArtifacts);
        var comments = await ReadCommentsAsync(repository, association.Number, cancellationToken)
            .ConfigureAwait(false);
        var existing = comments.FirstOrDefault(comment =>
            string.Equals(comment.User?.Login, "github-actions[bot]", StringComparison.OrdinalIgnoreCase)
            && comment.Body?.Contains(content.Marker, StringComparison.Ordinal) == true);

        if (existing is not null)
        {
            if (GitHubArtifactCommentFormatter.TryReadRunIdentity(
                    existing.Body,
                    out var existingRunId,
                    out var existingRunAttempt)
                && GitHubArtifactCommentFormatter.CompareRunIdentity(
                    existingRunId,
                    existingRunAttempt,
                    workflowEvent.RunId,
                    runAttempt) > 0)
            {
                return GitHubArtifactCommentResult.Skip(
                    $"The existing bot comment already records newer run {existingRunId}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            await _client.Issue.Comment
                .Update(repository.Owner, repository.Repository, existing.Id, content.Body)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return GitHubArtifactCommentResult.Success(
                association.Number,
                runAttempt,
                $"Updated artifact comment {existing.Id} for PR #{association.Number}.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var created = await _client.Issue.Comment
            .Create(repository.Owner, repository.Repository, association.Number, content.Body)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return GitHubArtifactCommentResult.Success(
            association.Number,
            runAttempt,
            $"Created artifact comment {created.Id} for PR #{association.Number}.");
    }

    private static string? ValidateCurrentRun(
        GitHubWorkflowRunEvent workflowEvent,
        WorkflowRun currentRun,
        GitHubWorkflowRunPullRequestAssociation association,
        long runAttempt)
    {
        if (runAttempt <= 0
            || currentRun.Id != workflowEvent.RunId
            || !string.Equals(
                currentRun.Repository?.FullName,
                workflowEvent.Repository.FullName,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(currentRun.Event, "pull_request_target", StringComparison.Ordinal)
            || currentRun.WorkflowId != workflowEvent.WorkflowId
            || (currentRun.RunAttempt <= 0 ? 1 : currentRun.RunAttempt) != runAttempt
            || !string.Equals(currentRun.Status.StringValue, "completed", StringComparison.Ordinal))
        {
            return "The workflow run identity could not be re-confirmed through the Actions API.";
        }

        var confirmedAssociations = currentRun.PullRequests ?? [];
        if (confirmedAssociations.Count != 1
            || confirmedAssociations[0].Number != association.Number
            || !GitObjectId.TryParse(confirmedAssociations[0].Head?.Sha, out var confirmedHeadSha)
            || confirmedHeadSha != association.HeadSha
            || !string.Equals(
                confirmedAssociations[0].Base?.Ref,
                association.BaseReference,
                StringComparison.Ordinal))
        {
            return "The workflow run pull request association could not be re-confirmed through the Actions API.";
        }

        if (currentRun.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return "The workflow run creation time is invalid.";
        }

        return null;
    }

    private static string? ValidatePullRequest(
        PullRequest pullRequest,
        GitHubWorkflowRunPullRequestAssociation association,
        DateTimeOffset runCreatedAt)
    {
        if (!string.Equals(pullRequest.State.StringValue, "open", StringComparison.Ordinal))
        {
            return "The associated pull request is no longer open.";
        }
        if (!SupportedBases.Contains(pullRequest.Base?.Ref ?? string.Empty))
        {
            return $"The pull request base '{pullRequest.Base?.Ref ?? "unknown"}' is not supported.";
        }
        if (!GitObjectId.TryParse(pullRequest.Head?.Sha, out var currentHeadSha)
            || currentHeadSha != association.HeadSha)
        {
            return "The workflow run source head no longer matches the current pull request head.";
        }
        if (!string.Equals(pullRequest.Base?.Ref, association.BaseReference, StringComparison.Ordinal))
        {
            return "The workflow run base no longer matches the current pull request base.";
        }
        if (runCreatedAt < pullRequest.CreatedAt)
        {
            return "The workflow run predates the associated pull request.";
        }

        return null;
    }

    private async Task<PullRequest> ReadPullRequestAsync(
        GitHubRepositoryAddress repository,
        int pullRequestNumber,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _client.PullRequest
            .Get(repository.Owner, repository.Repository, pullRequestNumber)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<Artifact>> ReadArtifactsAsync(
        GitHubRepositoryAddress repository,
        long runId,
        CancellationToken cancellationToken)
    {
        var artifacts = new List<Artifact>();
        for (var page = 1; ; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await _client.Actions.Artifacts
                .ListWorkflowArtifacts(
                    repository.Owner,
                    repository.Repository,
                    runId,
                    new ListArtifactsRequest
                    {
                        Page = page,
                        PerPage = PageSize,
                    })
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            artifacts.AddRange(response.Artifacts);
            if (response.Artifacts.Count < PageSize || artifacts.Count >= response.TotalCount)
            {
                return artifacts;
            }
        }
    }

    private async Task<WorkflowRun?> FindNewerAssociatedRunAsync(
        GitHubRepositoryAddress repository,
        WorkflowRun currentRun,
        int pullRequestNumber,
        long runAttempt,
        CancellationToken cancellationToken)
    {
        for (var page = 1; ; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await _client.Actions.Workflows.Runs
                .ListByWorkflow(
                    repository.Owner,
                    repository.Repository,
                    currentRun.WorkflowId,
                    new WorkflowRunsRequest
                    {
                        Event = "pull_request_target",
                        Created = $">={currentRun.CreatedAt.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}",
                    },
                    new ApiOptions
                    {
                        StartPage = page,
                        PageCount = 1,
                        PageSize = PageSize,
                    })
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            var newer = response.WorkflowRuns.FirstOrDefault(candidate =>
            {
                var candidateAttempt = candidate.RunAttempt <= 0 ? 1 : candidate.RunAttempt;
                if (candidate.Id == currentRun.Id)
                {
                    return candidateAttempt > runAttempt;
                }
                if (GitHubArtifactCommentFormatter.CompareRunIdentity(
                        candidate.Id,
                        candidateAttempt,
                        currentRun.Id,
                        runAttempt) <= 0)
                {
                    return false;
                }

                var associations = candidate.PullRequests ?? [];
                return associations.Count == 1 && associations[0].Number == pullRequestNumber;
            });
            if (newer is not null)
            {
                return newer;
            }
            if (response.WorkflowRuns.Count < PageSize || page * PageSize >= response.TotalCount)
            {
                return null;
            }
        }
    }

    private async Task<IReadOnlyList<IssueComment>> ReadCommentsAsync(
        GitHubRepositoryAddress repository,
        int pullRequestNumber,
        CancellationToken cancellationToken)
    {
        var comments = new List<IssueComment>();
        for (var page = 1; ; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await _client.Issue.Comment
                .GetAllForIssue(
                    repository.Owner,
                    repository.Repository,
                    pullRequestNumber,
                    new ApiOptions
                    {
                        StartPage = page,
                        PageCount = 1,
                        PageSize = PageSize,
                    })
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            comments.AddRange(response);
            if (response.Count < PageSize)
            {
                return comments;
            }
        }
    }

    private static IGitHubClient CreateClient(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(BuilderResources.GitHubTokenRequired);
        }

        return new GitHubClient(new ProductHeaderValue("WpfReorganize-Builder"))
        {
            Credentials = new Credentials(token),
        };
    }
}

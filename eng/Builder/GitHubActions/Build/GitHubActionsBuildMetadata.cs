namespace WpfReorganize.Builder;

using System.Text.Json;

internal sealed record GitHubActionsBuildMetadata
{
    private static readonly HashSet<string> SupportedEvents =
    [
        "pull_request_target",
        "push",
        "workflow_dispatch",
    ];

    private GitHubActionsBuildMetadata(
        string eventName,
        GitObjectId trustedSha,
        int? pullRequestNumber,
        GitObjectId? sourceHeadSha)
    {
        EventName = eventName;
        TrustedSha = trustedSha;
        PullRequestNumber = pullRequestNumber;
        SourceHeadSha = sourceHeadSha;
    }

    public string EventName { get; }

    public GitObjectId TrustedSha { get; }

    public int? PullRequestNumber { get; }

    public GitObjectId? SourceHeadSha { get; }

    public bool IsPullRequest => EventName == "pull_request_target";

    public static GitHubActionsBuildMetadata Read(
        string eventPath,
        string eventName,
        string trustedSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventPath);
        if (!File.Exists(eventPath))
        {
            throw new FileNotFoundException(BuilderResources.GitHubActionsEventFileNotFound, eventPath);
        }

        using var stream = File.OpenRead(eventPath);
        using var document = JsonDocument.Parse(stream);
        if (eventName != "pull_request_target")
        {
            return Create(eventName, trustedSha, null, null);
        }

        if (!document.RootElement.TryGetProperty("pull_request", out var pullRequest)
            || !pullRequest.TryGetProperty("number", out var numberElement)
            || !numberElement.TryGetInt32(out var pullRequestNumber)
            || !pullRequest.TryGetProperty("head", out var head)
            || !head.TryGetProperty("sha", out var shaElement)
            || shaElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(BuilderResources.InvalidPullRequestTargetEvent);
        }

        return Create(eventName, trustedSha, pullRequestNumber, shaElement.GetString());
    }

    public static GitHubActionsBuildMetadata Create(
        string eventName,
        string trustedSha,
        int? pullRequestNumber,
        string? sourceHeadSha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedSha);

        eventName = eventName.Trim();
        if (!SupportedEvents.Contains(eventName))
        {
            throw new ArgumentException(
                string.Format(BuilderResources.UnsupportedGitHubActionsEvent, eventName),
                nameof(eventName));
        }

        var parsedTrustedSha = GitObjectId.Parse(trustedSha);
        if (eventName == "pull_request_target")
        {
            if (pullRequestNumber is null or <= 0 || string.IsNullOrWhiteSpace(sourceHeadSha))
            {
                throw new ArgumentException(BuilderResources.PullRequestBuildMetadataRequired);
            }

            return new GitHubActionsBuildMetadata(
                eventName,
                parsedTrustedSha,
                pullRequestNumber,
                GitObjectId.Parse(sourceHeadSha));
        }

        if (pullRequestNumber is not null || !string.IsNullOrWhiteSpace(sourceHeadSha))
        {
            throw new ArgumentException(BuilderResources.NonPullRequestBuildMetadataNotAllowed);
        }

        return new GitHubActionsBuildMetadata(eventName, parsedTrustedSha, null, null);
    }
}

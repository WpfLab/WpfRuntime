namespace WpfReorganize.Builder;

using System.Text.Json;

internal sealed record GitHubWorkflowRunPullRequestAssociation(
    int Number,
    GitObjectId HeadSha,
    string BaseReference);

internal sealed record GitHubWorkflowRunEvent(
    GitHubRepositoryAddress Repository,
    long RunId,
    long WorkflowId,
    long? RunAttempt,
    string EventName,
    string? Conclusion,
    string Status,
    DateTimeOffset CreatedAt,
    GitHubRepositoryAddress RunRepository,
    IReadOnlyList<GitHubWorkflowRunPullRequestAssociation> PullRequests)
{
    public static GitHubWorkflowRunEvent Read(string eventPath, string repositoryFullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventPath);
        if (!File.Exists(eventPath))
        {
            throw new FileNotFoundException(BuilderResources.GitHubActionsEventFileNotFound, eventPath);
        }

        var expectedRepository = GitHubRepositoryAddress.ParseFullName(repositoryFullName);
        using var stream = File.OpenRead(eventPath);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var run = GetRequiredObject(root, "workflow_run");
        var eventRepository = ParseRepository(GetRequiredObject(root, "repository"));
        if (!string.Equals(eventRepository.FullName, expectedRepository.FullName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(BuilderResources.WorkflowRunRepositoryMismatch);
        }

        return new GitHubWorkflowRunEvent(
            expectedRepository,
            GetPositiveInt64(run, "id"),
            GetPositiveInt64(run, "workflow_id"),
            GetOptionalPositiveInt64(run, "run_attempt"),
            GetRequiredString(run, "event"),
            GetOptionalString(run, "conclusion"),
            GetRequiredString(run, "status"),
            GetRequiredDateTimeOffset(run, "created_at"),
            ParseRepository(GetRequiredObject(run, "repository")),
            ReadAssociations(run));
    }

    private static IReadOnlyList<GitHubWorkflowRunPullRequestAssociation> ReadAssociations(JsonElement run)
    {
        if (!run.TryGetProperty("pull_requests", out var pullRequests)
            || pullRequests.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var associations = new List<GitHubWorkflowRunPullRequestAssociation>();
        foreach (var pullRequest in pullRequests.EnumerateArray())
        {
            if (!pullRequest.TryGetProperty("number", out var numberElement)
                || !numberElement.TryGetInt32(out var number)
                || number <= 0
                || !pullRequest.TryGetProperty("head", out var head)
                || !head.TryGetProperty("sha", out var shaElement)
                || shaElement.ValueKind != JsonValueKind.String
                || !GitObjectId.TryParse(shaElement.GetString(), out var headSha)
                || !pullRequest.TryGetProperty("base", out var baseElement)
                || !baseElement.TryGetProperty("ref", out var baseReferenceElement)
                || baseReferenceElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(baseReferenceElement.GetString()))
            {
                throw new InvalidDataException(BuilderResources.InvalidWorkflowRunPullRequestAssociation);
            }

            associations.Add(new GitHubWorkflowRunPullRequestAssociation(
                number,
                headSha,
                baseReferenceElement.GetString()!));
        }

        return associations;
    }

    private static GitHubRepositoryAddress ParseRepository(JsonElement repository) =>
        GitHubRepositoryAddress.ParseFullName(GetRequiredString(repository, "full_name"));

    private static JsonElement GetRequiredObject(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(string.Format(BuilderResources.InvalidWorkflowRunEventProperty, name));
        }

        return value;
    }

    private static string GetRequiredString(JsonElement parent, string name)
    {
        var value = GetOptionalString(parent, name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException(string.Format(BuilderResources.InvalidWorkflowRunEventProperty, name))
            : value;
    }

    private static string? GetOptionalString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(string.Format(BuilderResources.InvalidWorkflowRunEventProperty, name));
        }

        return value.GetString();
    }

    private static long GetPositiveInt64(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value)
            || !value.TryGetInt64(out var number)
            || number <= 0)
        {
            throw new InvalidDataException(string.Format(BuilderResources.InvalidWorkflowRunEventProperty, name));
        }

        return number;
    }

    private static long? GetOptionalPositiveInt64(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }
        if (!value.TryGetInt64(out var number) || number <= 0)
        {
            throw new InvalidDataException(string.Format(BuilderResources.InvalidWorkflowRunEventProperty, name));
        }

        return number;
    }

    private static DateTimeOffset GetRequiredDateTimeOffset(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || !value.TryGetDateTimeOffset(out var dateTimeOffset))
        {
            throw new InvalidDataException(string.Format(BuilderResources.InvalidWorkflowRunEventProperty, name));
        }

        return dateTimeOffset;
    }
}

namespace WpfReorganize.Builder;

internal sealed record GitHubWorkflowRunPullRequestAssociation
(
    int Number,
    GitObjectId HeadSha,
    string BaseReference
);
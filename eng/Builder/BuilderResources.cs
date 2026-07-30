using System.Resources;

namespace WpfReorganize.Builder;

internal static class BuilderResources
{
    private static readonly ResourceManager ResourceManager =
        new("WpfReorganize.Builder.Resources", typeof(BuilderResources).Assembly);

    public static string InvalidPullRequestUrl => GetString(nameof(InvalidPullRequestUrl));

    public static string InvalidGitHubRemote => GetString(nameof(InvalidGitHubRemote));

    public static string TargetRemoteMismatch => GetString(nameof(TargetRemoteMismatch));

    public static string InvalidGitObjectId => GetString(nameof(InvalidGitObjectId));

    public static string InvalidKeepWorkspacePolicy => GetString(nameof(InvalidKeepWorkspacePolicy));

    public static string UntrustedBuildConsentRequired => GetString(nameof(UntrustedBuildConsentRequired));

    public static string GitHubTokenRequired => GetString(nameof(GitHubTokenRequired));

    public static string GitPushAuthenticationMayPrompt => GetString(nameof(GitPushAuthenticationMayPrompt));

    public static string PullRequestNotOpen => GetString(nameof(PullRequestNotOpen));

    public static string TargetBranchSourceConflict => GetString(nameof(TargetBranchSourceConflict));

    public static string NoChangesToRelay => GetString(nameof(NoChangesToRelay));

    public static string UnsupportedGitHubActionsEvent => GetString(nameof(UnsupportedGitHubActionsEvent));

    public static string PullRequestBuildMetadataRequired => GetString(nameof(PullRequestBuildMetadataRequired));

    public static string NonPullRequestBuildMetadataNotAllowed => GetString(nameof(NonPullRequestBuildMetadataNotAllowed));

    public static string GitHubActionsEventFileNotFound => GetString(nameof(GitHubActionsEventFileNotFound));

    public static string InvalidPullRequestTargetEvent => GetString(nameof(InvalidPullRequestTargetEvent));

    public static string GitHubActionsOutputMustBeSingleLine => GetString(nameof(GitHubActionsOutputMustBeSingleLine));

    public static string InvalidGitHubActionsOutputName => GetString(nameof(InvalidGitHubActionsOutputName));

    public static string CheckoutPersistedHttpCredentials => GetString(nameof(CheckoutPersistedHttpCredentials));

    public static string CheckoutPersistedRemoteCredentials => GetString(nameof(CheckoutPersistedRemoteCredentials));

    public static string GitHubActionsOutputRequired => GetString(nameof(GitHubActionsOutputRequired));

    public static string InvalidGitHubActionsBuildTarget => GetString(nameof(InvalidGitHubActionsBuildTarget));

    public static string TestedCommitDoesNotMatchEventSha => GetString(nameof(TestedCommitDoesNotMatchEventSha));

    public static string TestedMergeDoesNotMatchPullRequestEvent => GetString(nameof(TestedMergeDoesNotMatchPullRequestEvent));

    public static string MsBuildNotFound => GetString(nameof(MsBuildNotFound));

    public static string GitHubActionsEnvironmentVariableRequired => GetString(nameof(GitHubActionsEnvironmentVariableRequired));

    public static string InvalidGitHubRepositoryFullName => GetString(nameof(InvalidGitHubRepositoryFullName));

    public static string WorkflowRunRepositoryMismatch => GetString(nameof(WorkflowRunRepositoryMismatch));

    public static string InvalidWorkflowRunPullRequestAssociation => GetString(nameof(InvalidWorkflowRunPullRequestAssociation));

    public static string InvalidWorkflowRunEventProperty => GetString(nameof(InvalidWorkflowRunEventProperty));

    private static string GetString(string name) =>
        ResourceManager.GetString(name) ?? throw new MissingManifestResourceException($"Resource '{name}' was not found.");
}

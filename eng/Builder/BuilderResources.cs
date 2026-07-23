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

    public static string DetachedHeadRequiresBase => GetString(nameof(DetachedHeadRequiresBase));

    public static string PullRequestNotOpen => GetString(nameof(PullRequestNotOpen));

    public static string TargetBranchSourceConflict => GetString(nameof(TargetBranchSourceConflict));

    public static string NoChangesToRelay => GetString(nameof(NoChangesToRelay));

    private static string GetString(string name) =>
        ResourceManager.GetString(name) ?? throw new MissingManifestResourceException($"Resource '{name}' was not found.");
}

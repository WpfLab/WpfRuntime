namespace WpfReorganize.Builder;

internal sealed record GitHubRepositoryAddress(string Owner, string Repository)
{
    public string FullName => $"{Owner}/{Repository}";

    public string HttpsUrl => $"https://github.com/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Repository)}.git";

    public static GitHubRepositoryAddress ParseRemote(string value)
    {
        if (!TryParseRemote(value, out var address))
        {
            throw new ArgumentException(BuilderResources.InvalidGitHubRemote, nameof(value));
        }

        return address;
    }

    public static bool TryParseRemote(string? value, out GitHubRepositoryAddress address)
    {
        address = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string path;
        if (value.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            path = value["git@github.com:".Length..];
        }
        else if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, "ssh", StringComparison.OrdinalIgnoreCase))
            && string.Equals(uri.IdnHost, "github.com", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? string.IsNullOrEmpty(uri.UserInfo) && (uri.IsDefaultPort || uri.Port == 443)
                : (string.IsNullOrEmpty(uri.UserInfo) || string.Equals(uri.UserInfo, "git", StringComparison.OrdinalIgnoreCase))
                    && (uri.IsDefaultPort || uri.Port == 22)))
        {
            path = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
        }
        else
        {
            return false;
        }

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2
            || !PullRequestAddress.TryDecodeRepositorySegment(segments[0], out var owner)
            || !PullRequestAddress.TryDecodeRepositorySegment(RemoveGitSuffix(segments[1]), out var repository))
        {
            return false;
        }

        address = new GitHubRepositoryAddress(owner, repository);
        return true;
    }

    public static GitHubRepositoryAddress FromMatchingRemotes(string fetchUrl, string pushUrl)
    {
        var fetchAddress = ParseRemote(fetchUrl);
        var pushAddress = ParseRemote(pushUrl);
        if (!string.Equals(fetchAddress.FullName, pushAddress.FullName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(BuilderResources.TargetRemoteMismatch);
        }

        return fetchAddress;
    }

    private static string RemoveGitSuffix(string value) =>
        value.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
}

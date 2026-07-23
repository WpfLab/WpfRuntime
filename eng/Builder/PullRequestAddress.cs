using System.Globalization;

namespace WpfReorganize.Builder;

internal sealed record PullRequestAddress(string Owner, string Repository, int Number)
{
    public string CanonicalUrl => $"https://github.com/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Repository)}/pull/{Number}";

    public string SourceKey => $"{Owner}/{Repository}#{Number}";

    public static PullRequestAddress Parse(string value)
    {
        if (!TryParse(value, out var address))
        {
            throw new ArgumentException(BuilderResources.InvalidPullRequestUrl, nameof(value));
        }

        return address;
    }

    public static bool TryParse(string? value, out PullRequestAddress address)
    {
        address = null!;
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains('\\')
            || !HasValidPercentEncoding(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.IdnHost, "github.com", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || (!uri.IsDefaultPort && uri.Port != 443))
        {
            return false;
        }

        var segments = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 4
            || !string.Equals(segments[2], "pull", StringComparison.OrdinalIgnoreCase)
            || !TryDecodeRepositorySegment(segments[0], out var owner)
            || !TryDecodeRepositorySegment(segments[1], out var repository)
            || !int.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || number <= 0)
        {
            return false;
        }

        address = new PullRequestAddress(owner, repository, number);
        return true;
    }

    internal static bool TryDecodeRepositorySegment(string value, out string decoded)
    {
        decoded = string.Empty;
        if (!HasValidPercentEncoding(value))
        {
            return false;
        }

        try
        {
            decoded = Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(decoded)
            && decoded is not "." and not ".."
            && decoded.All(character =>
                char.IsAsciiLetterOrDigit(character)
                || character is '-' or '_' or '.')
            && !decoded.Any(character =>
                char.IsControl(character)
                || char.IsWhiteSpace(character)
                || character is '/' or '\\');
    }

    private static bool HasValidPercentEncoding(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length
                || !Uri.IsHexDigit(value[index + 1])
                || !Uri.IsHexDigit(value[index + 2]))
            {
                return false;
            }

            index += 2;
        }

        return true;
    }
}

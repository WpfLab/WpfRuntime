using System.Net;
using Octokit;

namespace WpfReorganize.Builder;

internal sealed class GitHubPullRequestService
{
    private const int PageSize = 100;
    private readonly GitHubClient _client;

    public GitHubPullRequestService(string token)
        : this(CreateClient(token))
    {
    }

    internal GitHubPullRequestService(GitHubClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public async Task<PullRequestSource> GetSourceAsync(
        PullRequestAddress address,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        cancellationToken.ThrowIfCancellationRequested();
        var pullRequest = await _client.PullRequest
            .Get(address.Owner, address.Repository, address.Number)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        var commits = await _client.PullRequest
            .Commits(address.Owner, address.Repository, address.Number)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var baseRepository = pullRequest.Base.Repository
            ?? throw new InvalidOperationException("The source pull request base repository is unavailable.");
        var baseAddress = ParseFullName(baseRepository.FullName);
        var baseCloneAddress = GitHubRepositoryAddress.ParseRemote(baseRepository.CloneUrl);
        if (!string.Equals(baseAddress.FullName, baseCloneAddress.FullName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("GitHub returned inconsistent base repository metadata.");
        }
        var headRepository = pullRequest.Head.Repository;
        GitHubRepositoryAddress? headAddress = null;
        string? headCloneUrl = null;
        if (headRepository is not null)
        {
            headAddress = ParseFullName(headRepository.FullName);
            var cloneAddress = GitHubRepositoryAddress.ParseRemote(headRepository.CloneUrl);
            if (!string.Equals(headAddress.FullName, cloneAddress.FullName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("GitHub returned inconsistent source repository metadata.");
            }

            headCloneUrl = headRepository.CloneUrl;
        }
        var mappedAddress = PullRequestAddress.TryParse(pullRequest.HtmlUrl, out var htmlAddress)
            ? htmlAddress
            : address;
        var commitShas = commits
            .Select(commit => GitObjectId.Parse(commit.Sha))
            .ToHashSet();

        return new PullRequestSource(
            mappedAddress,
            pullRequest.Title,
            pullRequest.State.ToString(),
            pullRequest.Draft,
            headAddress,
            headCloneUrl,
            pullRequest.Head.Ref,
            GitObjectId.Parse(pullRequest.Head.Sha),
            baseAddress,
            baseRepository.CloneUrl,
            pullRequest.Base.Ref,
            GitObjectId.Parse(pullRequest.Base.Sha),
            commitShas);
    }

    public async Task<PullRequestSource> RefreshSourceOnceAsync(
        PullRequestSource expected,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var refreshed = await GetSourceAsync(expected.Address, cancellationToken).ConfigureAwait(false);
        return refreshed;
    }

    public async Task<Uri?> FindMatchingOpenTargetPullRequestAsync(
        TargetRepository target,
        PullRequestAddress source,
        CancellationToken cancellationToken)
    {
        var pullRequest = await FindOpenTargetPullRequestAsync(target, source, cancellationToken).ConfigureAwait(false);
        return pullRequest is null ? null : new Uri(pullRequest.HtmlUrl);
    }

    public async Task<Uri> CreateOrReuseTargetPullRequestAsync(
        TargetRepository target,
        PullRequestSource source,
        DateTimeOffset validationCompletedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        var existing = await FindOpenTargetPullRequestAsync(target, source.Address, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return new Uri(existing.HtmlUrl);
        }

        var title = CreateTitle(source);
        var body = CreateBody(target, source, validationCompletedAtUtc);
        var newPullRequest = new NewPullRequest(title, $"{target.Address.Owner}:{target.RelayBranch}", target.BaseBranch)
        {
            Body = body,
            MaintainerCanModify = false,
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var created = await _client.PullRequest
                .Create(target.Address.Owner, target.Address.Repository, newPullRequest)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new Uri(created.HtmlUrl);
        }
        catch (ApiValidationException exception) when (exception.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var raced = await FindOpenTargetPullRequestAsync(target, source.Address, cancellationToken)
                .ConfigureAwait(false);
            if (raced is not null)
            {
                return new Uri(raced.HtmlUrl);
            }

            throw;
        }
    }

    internal static string CreateTitle(PullRequestSource source)
    {
        var title = SanitizeInlineText(source.Title, 160);
        return $"[PR relay] {source.Address.Owner}/{source.Address.Repository}#{source.Address.Number}: {title}";
    }

    internal static string CreateBody(
        TargetRepository target,
        PullRequestSource source,
        DateTimeOffset validationCompletedAtUtc) =>
        $"Relays {source.Address.CanonicalUrl}.\n\n" +
        $"- Source base: `{EscapeCode(source.BaseRepository.FullName)}:{EscapeCode(source.BaseReference)}` at `{source.BaseSha}`\n" +
        $"- Source head: `{EscapeCode(source.HeadRepository?.FullName ?? "deleted-source-repository")}:{EscapeCode(source.HeadReference)}` at `{source.HeadSha}`\n" +
        $"- Target: `{EscapeCode(target.Address.FullName)}:{EscapeCode(target.BaseBranch)}` <- `{EscapeCode(target.RelayBranch)}`\n" +
        $"- Local validation completed: `{validationCompletedAtUtc:O}`\n" +
        "- GitHub Actions build results will be written back by the trusted metadata workflow.\n\n" +
        $"Source-PR: {source.Address.CanonicalUrl}\n" +
        $"Source-Head-SHA: {source.HeadSha}\n\n" +
        RelayMarkers.CreatePullRequestMarker(source.Address);

    private static GitHubClient CreateClient(string token)
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

    private async Task<PullRequest?> FindOpenTargetPullRequestAsync(
        TargetRepository target,
        PullRequestAddress source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        var request = new PullRequestRequest
        {
            State = ItemStateFilter.Open,
            Head = $"{target.Address.Owner}:{target.RelayBranch}",
            Base = target.BaseBranch,
        };
        var options = new ApiOptions { PageSize = PageSize, PageCount = 1 };
        cancellationToken.ThrowIfCancellationRequested();
        var pullRequests = await _client.PullRequest
            .GetAllForRepository(target.Address.Owner, target.Address.Repository, request, options)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var pullRequest in pullRequests)
        {
            if (!string.Equals(pullRequest.Head.Ref, target.RelayBranch, StringComparison.Ordinal)
                || !string.Equals(pullRequest.Base.Ref, target.BaseBranch, StringComparison.Ordinal))
            {
                continue;
            }

            if (!RelayMarkers.PullRequestBodyMatches(pullRequest.Body, source))
            {
                throw new InvalidOperationException(BuilderResources.TargetBranchSourceConflict);
            }

            return pullRequest;
        }

        return null;
    }

    private static GitHubRepositoryAddress ParseFullName(string fullName)
    {
        var segments = fullName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            throw new InvalidOperationException($"GitHub returned an invalid repository name: {fullName}");
        }

        if (!GitHubRepositoryAddress.TryParseRemote($"https://github.com/{segments[0]}/{segments[1]}.git", out var address))
        {
            throw new InvalidOperationException($"GitHub returned an invalid repository name: {fullName}");
        }

        return address;
    }

    private static string SanitizeInlineText(string value, int maximumLength)
    {
        var sanitized = new string(value
            .Where(character => !char.IsControl(character))
            .Select(character => char.IsWhiteSpace(character) ? ' ' : character)
            .ToArray());
        sanitized = string.Join(' ', sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (sanitized.Length > maximumLength)
        {
            sanitized = sanitized[..maximumLength];
        }

        return sanitized;
    }

    private static string EscapeCode(string value) =>
        SanitizeInlineText(value.Replace("`", "'", StringComparison.Ordinal), 240);
}

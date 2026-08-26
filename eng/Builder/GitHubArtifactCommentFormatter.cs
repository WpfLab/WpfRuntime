namespace WpfReorganize.Builder;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Octokit;

internal sealed record GitHubArtifactCommentItem(
    long Id,
    string Name,
    int SizeInBytes,
    DateTime ExpiresAt,
    GitObjectId TestedSha,
    string PackageVersion);

internal sealed record GitHubArtifactCommentContent(
    string Marker,
    string RunMarker,
    string Body,
    bool HasValidSuccessArtifacts,
    GitObjectId? TestedSha);

internal static class GitHubArtifactCommentFormatter
{
    private const int MaxArtifacts = 5;
    private static readonly Regex RunMarkerRegex = new(
        "<!-- wpf-nuget-artifacts-run id=(\\d+) attempt=(\\d+) -->",
        RegexOptions.CultureInvariant);

    public static GitHubArtifactCommentContent Create(
        GitHubRepositoryAddress repository,
        int pullRequestNumber,
        GitObjectId currentPullRequestHead,
        long runId,
        long runAttempt,
        string conclusion,
        IReadOnlyList<GitHubArtifactCommentItem> artifacts)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(conclusion);
        ArgumentNullException.ThrowIfNull(artifacts);
        if (pullRequestNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pullRequestNumber));
        }
        if (runId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runId));
        }
        if (runAttempt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runAttempt));
        }

        var testedShas = artifacts.Select(artifact => artifact.TestedSha).Distinct().ToArray();
        GitObjectId? testedSha = testedShas.Length == 1 ? testedShas[0] : null;
        var hasValidSuccessArtifacts = string.Equals(conclusion, "success", StringComparison.Ordinal)
            && artifacts.Count > 0
            && testedSha is not null;
        var resultLabel = hasValidSuccessArtifacts
            ? "Succeeded"
            : string.Equals(conclusion, "success", StringComparison.Ordinal)
                ? "Invalid: the workflow succeeded, but no unique valid nupkg artifact was found"
                : GetConclusionLabel(conclusion);
        var marker = CreateMarker(pullRequestNumber);
        var runMarker = CreateRunMarker(runId, runAttempt);
        var runUrl = $"https://github.com/{repository.Owner}/{repository.Repository}/actions/runs/{runId}";
        var body = new List<string>
        {
            marker,
            runMarker,
            "## WPF NuGet Build",
            string.Empty,
            $"- Result: {resultLabel}",
            $"- PR head: `{currentPullRequestHead}`",
            $"- Tested merge commit: {(testedSha is null ? "No verifiable artifact identity" : $"`{testedSha}`")}",
            $"- Actions run: [{runId} (attempt {runAttempt})]({runUrl})",
        };

        if (hasValidSuccessArtifacts)
        {
            var packageVersions = artifacts.Select(artifact => artifact.PackageVersion).Distinct(StringComparer.Ordinal).ToArray();
            if (packageVersions.Length == 1)
            {
                var packageVersion = packageVersions[0];
                var packageUrl = $"https://www.nuget.org/packages/{PackageMetadata.Id}/{Uri.EscapeDataString(packageVersion)}";
                body.Add($"- Published NuGet: [{PackageMetadata.Id} {packageVersion}]({packageUrl})");
            }

            foreach (var artifact in artifacts.Take(MaxArtifacts))
            {
                var artifactUrl = $"{runUrl}/artifacts/{artifact.Id}";
                var (artifactLabel, fileName) = GetArtifactPresentation(artifact.Name);
                body.Add($"- {artifactLabel}: [{EscapeMarkdown(fileName)}]({artifactUrl})");
                body.Add($"  - Size: {FormatBytes(artifact.SizeInBytes)}");
                body.Add($"  - Expires at: {EscapeMarkdown(artifact.ExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}");
            }

            if (artifacts.Count > MaxArtifacts)
            {
                body.Add($"- {artifacts.Count - MaxArtifacts} additional valid artifact(s) are not shown.");
            }
        }

        return new GitHubArtifactCommentContent(
            marker,
            runMarker,
            string.Join('\n', body),
            hasValidSuccessArtifacts,
            testedSha);
    }

    private static (string Label, string FileName) GetArtifactPresentation(string artifactName)
    {
        if (artifactName.EndsWith(".symbols.zip", StringComparison.Ordinal))
        {
            return ("PDB symbols archive", artifactName);
        }

        if (artifactName.EndsWith(".snupkg", StringComparison.Ordinal))
        {
            return ("NuGet symbol package", artifactName);
        }

        return ("NuGet package", artifactName);
    }

    public static IReadOnlyList<GitHubArtifactCommentItem> FilterArtifacts(
        IEnumerable<Artifact> artifacts,
        int pullRequestNumber,
        long runId,
        long runAttempt)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        var pattern = new Regex(
            $"^{Regex.Escape(PackageMetadata.Id)}-nupkg-pr-{pullRequestNumber}-sha-([0-9a-fA-F]{{40}})-run-{runId}-attempt-{runAttempt}-version-([0-9A-Za-z.+-]+?)\\.(?:nupkg|snupkg|symbols\\.zip)$",
            RegexOptions.CultureInvariant);
        return artifacts
            .Where(artifact => artifact is not null
                && !artifact.Expired
                && artifact.SizeInBytes > 0
                && artifact.Name?.StartsWith($"{PackageMetadata.Id}-nupkg-", StringComparison.Ordinal) == true)
            .Select(artifact => (Artifact: artifact, Match: pattern.Match(artifact.Name)))
            .Where(item => item.Match.Success)
            .Select(item => new GitHubArtifactCommentItem(
                item.Artifact.Id,
                item.Artifact.Name,
                item.Artifact.SizeInBytes,
                item.Artifact.ExpiresAt,
                GitObjectId.Parse(item.Match.Groups[1].Value),
                item.Match.Groups[2].Value))
            .OrderBy(item => item.Id)
            .ToArray();
    }

    public static string CreateMarker(int pullRequestNumber) =>
        $"<!-- wpf-nuget-artifacts workflow=build pr={pullRequestNumber} -->";

    public static string CreateRunMarker(long runId, long runAttempt) =>
        $"<!-- wpf-nuget-artifacts-run id={runId} attempt={runAttempt} -->";

    public static bool TryReadRunIdentity(string? body, out long runId, out long runAttempt)
    {
        runId = 0;
        runAttempt = 0;
        var match = RunMarkerRegex.Match(body ?? string.Empty);
        return match.Success
            && long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out runId)
            && runId > 0
            && long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out runAttempt)
            && runAttempt > 0;
    }

    public static int CompareRunIdentity(long leftId, long leftAttempt, long rightId, long rightAttempt)
    {
        var idComparison = leftId.CompareTo(rightId);
        return idComparison != 0 ? idComparison : leftAttempt.CompareTo(rightAttempt);
    }

    public static string EscapeMarkdown(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var sanitized = new string(value.Where(character => !char.IsControl(character)).Take(120).ToArray());
        var builder = new StringBuilder(sanitized.Length * 2);
        foreach (var character in sanitized)
        {
            if (character == '\\')
            {
                builder.Append("\\\\");
            }
            else if (character == '@')
            {
                builder.Append("@\u200b");
            }
            else
            {
                if ("`*_[\\]()<>{}#+.!|~-".Contains(character))
                {
                    builder.Append('\\');
                }
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    public static string FormatBytes(long value)
    {
        if (value < 0)
        {
            return "unknown";
        }

        string[] units = ["B", "KiB", "MiB", "GiB"];
        var amount = (double)value;
        var unitIndex = 0;
        while (amount >= 1024 && unitIndex < units.Length - 1)
        {
            amount /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? amount.ToString("F0", CultureInfo.InvariantCulture) + $" {units[unitIndex]}"
            : amount.ToString("F1", CultureInfo.InvariantCulture) + $" {units[unitIndex]}";
    }

    private static string GetConclusionLabel(string conclusion) =>
        conclusion switch
        {
            "success" => "Succeeded",
            "failure" => "Failed",
            "cancelled" => "Cancelled",
            "timed_out" => "Timed out",
            "action_required" => "Action required",
            "neutral" => "Neutral",
            "skipped" => "Skipped",
            "stale" => "Stale",
            _ => EscapeMarkdown(conclusion),
        };

}

using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace WpfReorganize.Builder;

internal sealed class PublishPackagesService
(
    HttpClient client,
    Func<ProcessRunOptions, CancellationToken, Task<ProcessResult>> runProcess
)
{
    public async Task<int> RunAsync
    (
        GitHubRepositoryAddress repository,
        long runId,
        string outputDirectory,
        string nugetKey,
        string githubToken,
        CancellationToken cancellationToken
    )
    {
        Directory.CreateDirectory(outputDirectory);
        for (var page = 1;; page++)
        {
            using var response = await client.GetAsync
            (
                $"https://api.github.com/repos/{repository.FullName}/actions/runs/{runId}/artifacts?per_page=100&page={page}",
                HttpCompletionOption.ResponseHeadersRead, cancellationToken
            ).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync
                (stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var artifacts = document.RootElement.GetProperty("artifacts");
            foreach (var artifact in artifacts.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = artifact.GetProperty("name").GetString() ?? string.Empty;
                if (!name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = artifact.GetProperty("id").GetInt64();
                using var download = await client.GetAsync
                (
                    $"https://api.github.com/repos/{repository.FullName}/actions/artifacts/{id}/zip",
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken
                ).ConfigureAwait(false);
                download.EnsureSuccessStatusCode();
                await using var archiveStream = await download.Content.ReadAsStreamAsync
                    (cancellationToken).ConfigureAwait(false);
                using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
                // Flatten package entries rather than trusting paths from the downloaded archive.
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileName = Path.GetFileName(entry.FullName.Replace('\\', '/'));
                    if (!fileName.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) &&
                        !fileName.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    await using var source = entry.Open();
                    await using var destination = File.Create(Path.Join(outputDirectory, fileName));
                    await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                }
            }

            if (artifacts.GetArrayLength() < 100)
            {
                break;
            }
        }

        var packages = Directory.GetFiles(outputDirectory, "*.nupkg");
        if (packages.Length == 0)
        {
            Log.Info(BuilderResources.NoPackagesToPublish);
        }

        var failed = false;
        foreach (var package in packages)
        foreach (var (source, key) in new[]
                 {
                     ("https://api.nuget.org/v3/index.json", nugetKey),
                     ($"https://nuget.pkg.github.com/{repository.Owner}", githubToken),
                 })
        {
            var exitCode = -1;
            try
            {
                var result = await runProcess
                (
                    new ProcessRunOptions
                    (
                        "dotnet", outputDirectory, "nuget", "push", package, "--source", source, "--api-key", key
                    )
                    {
                        Timeout = TimeSpan.FromMinutes(5),
                    }, cancellationToken
                ).ConfigureAwait(false);
                exitCode = result.ExitCode;
            }
            catch (TimeoutException)
            {
                // Report below without ProcessRunner's credential-bearing arguments.
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            // Do not log process arguments or output: either can contain credentials.
            Log.Info(string.Format(BuilderResources.PackagePushResult, Path.GetFileName(package), source, exitCode));
            failed |= exitCode != 0;
        }

        return failed ? 1 : 0;
    }
}
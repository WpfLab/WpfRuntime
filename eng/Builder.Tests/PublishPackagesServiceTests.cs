using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class PublishPackagesServiceTests
{
    [Fact]
    public async Task PublishDownloadsExactRunAndPushesAllPackagesWithAdjacentSymbols()
    {
        var requests = new List<string>();
        using var client = CreateClient(requests);
        var pushes = new List<ProcessRunOptions>();
        var service = new PublishPackagesService(client, (options, _) =>
        {
            pushes.Add(options);
            return Task.FromResult(new ProcessResult(0, "", "", TimeSpan.Zero));
        });
        var directory = CreateDirectory();

        await service.RunAsync(new("owner", "repo"), 42, directory, "nuget-key", "github-key", default);

        Assert.Equal(new[]
        {
            "/repos/owner/repo/actions/runs/42/artifacts?per_page=100&page=1",
            "/repos/owner/repo/actions/artifacts/1/zip",
            "/repos/owner/repo/actions/artifacts/2/zip",
        }, requests);
        Assert.Equal(4, pushes.Count);
        Assert.True(File.Exists(Path.Join(directory, "arbitrary.snupkg")));
        Assert.All(pushes, push => Assert.Contains("push", push.Arguments));
        Assert.Equal(new[] { "nuget-key", "github-key", "nuget-key", "github-key" }, pushes.Select(push => push.Arguments[^1]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedNuGetPushStillAttemptsGitHubPackages(bool timeout)
    {
        using var client = CreateClient([]);
        var calls = 0;
        var service = new PublishPackagesService(client, (_, _) =>
        {
            calls++;
            if (timeout) return Task.FromException<ProcessResult>(new TimeoutException("secret"));
            return Task.FromResult(new ProcessResult(1, "secret", "secret", TimeSpan.Zero));
        });

        var result = await service.RunAsync(new("owner", "repo"), 42, CreateDirectory(), "nuget-key", "github-key", default);

        Assert.Equal(4, calls);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task NoPackagesReportsSuccessWithoutStartingPush()
    {
        using var client = new HttpClient(new ResponseHandler(_ => new StringContent("{\"artifacts\":[]}")));
        var service = new PublishPackagesService(client, (_, _) => throw new InvalidOperationException("Unexpected push"));

        var result = await service.RunAsync(new("owner", "repo"), 42, CreateDirectory(), "nuget-key", "github-key", default);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ArchivePathsCannotWriteOutsideOutputDirectory()
    {
        using var client = CreateClient([]);
        var directory = CreateDirectory();
        var service = new PublishPackagesService(client, (_, _) => Task.FromResult(new ProcessResult(0, "", "", TimeSpan.Zero)));

        await service.RunAsync(new("owner", "repo"), 42, directory, "nuget-key", "github-key", default);

        Assert.False(File.Exists(Path.Join(directory, "..", "arbitrary.nupkg")));
        Assert.False(File.Exists(Path.Join(directory, "execute.ps1")));
    }

    private static HttpClient CreateClient(List<string> requests) => new(new ResponseHandler(request =>
    {
        var path = request.RequestUri!.PathAndQuery;
        requests.Add(path);
        return path.Contains("/runs/", StringComparison.Ordinal)
            ? new StringContent("{\"artifacts\":[{\"id\":1,\"name\":\"anything.nupkg\"},{\"id\":2,\"name\":\"anything.snupkg\"}]}")
            : new ByteArrayContent(CreateArchive(path.Contains("/1/", StringComparison.Ordinal)
                ? ["../arbitrary.nupkg", "nested/second.nupkg", "execute.ps1"]
                : ["nested/arbitrary.snupkg"]));
    }));

    private static byte[] CreateArchive(string[] names)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in names)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes("package content"));
            }
        }
        return stream.ToArray();
    }

    private static string CreateDirectory() => Path.Join(Path.GetTempPath(), $"builder-publish-tests-{Guid.NewGuid():N}");

    private sealed class ResponseHandler(Func<HttpRequestMessage, HttpContent> content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content(request) });
    }
}

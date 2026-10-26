using System.IO.Compression;
using System.Xml.Linq;
using WpfReorganize.Builder;
using Xunit.Abstractions;

namespace WpfReorganize.Builder.Tests;

public sealed class WpfWinFormsCompatibilityTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(8, "8.0.101")]
    [InlineData(10, "10.0.100")]
    [Trait("Category", "PackageIntegration")]
    public async Task WpfAndWinFormsPreserveFrameworkAssembliesAsync(int majorVersion, string sdkVersion)
    {
        Assert.True(OperatingSystem.IsWindows(), $"This integration test requires Windows and the .NET {majorVersion} SDK/Desktop runtime.");
        var packagePath = Environment.GetEnvironmentVariable("WPF_RUNTIME_TEST_PACKAGE");
        Assert.True(!string.IsNullOrWhiteSpace(packagePath) && File.Exists(packagePath), "Set WPF_RUNTIME_TEST_PACKAGE to the freshly built WpfLab.WpfRuntime .nupkg.");
        using var package = ZipFile.OpenRead(packagePath!);
        Assert.DoesNotContain(package.Entries, entry => Path.GetFileName(entry.FullName) is "WpfRuntime.Windows.Primitives.dll" or "System.Windows.Primitives.dll");
        using var manifestStream = package.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
        var manifest = XDocument.Load(manifestStream);
        var version = manifest.Descendants().Single(element => element.Name.LocalName == "version").Value;
        Assert.Equal("WpfLab.WpfRuntime", manifest.Descendants().Single(element => element.Name.LocalName == "id").Value);

        var directory = Path.Join(Path.GetTempPath(), "WpfRuntimeCompatibility", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        output.WriteLine(directory);
        foreach (var path in Directory.GetFiles(Path.Join(AppContext.BaseDirectory, "WpfWinFormsNet10")))
            File.Copy(path, Path.Join(directory, Path.GetFileName(path)));
        var feed = Path.Join(directory, "feed");
        Directory.CreateDirectory(feed);
        File.Copy(packagePath!, Path.Join(feed, Path.GetFileName(packagePath!)));
        new XDocument(new XElement("configuration",
            new XElement("packageSources", new XElement("clear"),
                new XElement("add", new XAttribute("key", "local"), new XAttribute("value", feed)),
                new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", "https://api.nuget.org/v3/index.json"))),
            new XElement("packageSourceMapping",
                new XElement("packageSource", new XAttribute("key", "local"), new XElement("package", new XAttribute("pattern", "WpfLab.WpfRuntime"))),
                new XElement("packageSource", new XAttribute("key", "nuget.org"), new XElement("package", new XAttribute("pattern", "*"))))))
            .Save(Path.Join(directory, "NuGet.config"));
        await File.WriteAllTextAsync(Path.Join(directory, "global.json"), System.Text.Json.JsonSerializer.Serialize(
            new { sdk = new { version = sdkVersion, rollForward = "latestFeature" } }));
        await File.WriteAllBytesAsync(Path.Join(directory, "splash.png"), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="));

        var build = await ProcessRunner.RunAsync(new ProcessRunOptions("dotnet", directory,
            "build", "WpfWinFormsNet10.csproj", "-c", "Release", "-o", "output",
            $"-p:WpfCompatibilityTargetFramework=net{majorVersion}.0-windows", $"-p:WpfRuntimeTestVersion={version}", $"-p:RestorePackagesPath={Path.Join(directory, "packages")}")
        { Timeout = TimeSpan.FromMinutes(10) });
        output.WriteLine(build.StandardOutput);
        output.WriteLine(build.StandardError);
        Assert.True(build.ExitCode == 0, $"Build failed ({build.ExitCode}): {build.StandardOutput}\n{build.StandardError}");

        var run = await ProcessRunner.RunAsync(new ProcessRunOptions("dotnet", directory,
            Path.Join(directory, "output", "WpfWinFormsNet10.dll")) { Timeout = TimeSpan.FromMinutes(2) });
        output.WriteLine(run.StandardOutput);
        output.WriteLine(run.StandardError);
        Assert.True(run.ExitCode == 0 && run.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Contains($"WPF_WINFORMS_NET{majorVersion}_PASS"),
            $"Probe failed ({run.ExitCode}): {run.StandardOutput}\n{run.StandardError}");
    }
}

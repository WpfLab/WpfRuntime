using System.Xml.Linq;
using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class BuildCommandTests
{
    [Theory]
    [InlineData("1.0.0.1-test", "1.0.0.1-local")]
    [InlineData("1.1.0.1-test", "1.1.0.1-local")]
    [InlineData("1.1.0.1", "1.1.0.1-local")]
    public void DefaultVersionUsesConfiguredPrefix(string prefix, string expected)
    {
        var repositoryPath = Path.Join(Path.GetTempPath(), $"builder-version-{Guid.NewGuid():N}");
        var engPath = Path.Join(repositoryPath, "eng");
        Directory.CreateDirectory(engPath);
        var versionsPath = Path.Join(engPath, "Versions.props");
        new XDocument(
            new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement("WpfRuntimeCiNuGetVersionPrefix", prefix))))
            .Save(versionsPath);
        Console.WriteLine($"Version definition: {versionsPath}");
        var command = new BuildCommand();

        var version = command.ResolveVersion(repositoryPath);

        Assert.Equal(expected, version);
    }

    [Fact]
    public void ExplicitVersionDoesNotRequireVersionDefinition()
    {
        var command = new BuildCommand { Version = "2.0.0-preview.1" };
        var repositoryPath = Path.Join(Path.GetTempPath(), $"builder-version-{Guid.NewGuid():N}");

        var version = command.ResolveVersion(repositoryPath);

        Assert.Equal("2.0.0-preview.1", version);
    }
}

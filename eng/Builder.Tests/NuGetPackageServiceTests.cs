using System.Xml.Linq;
using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class NuGetPackageServiceTests
{
    [Fact]
    public void GenerateSymbolNuspecIncludesMultiplePdbFilesWithRuntimePaths()
    {
        var stagingDirectory = CreateStagingDirectory();
        WritePdb(stagingDirectory, "win-x64", "PresentationCore.pdb");
        WritePdb(stagingDirectory, "win-x64", "PresentationFramework.pdb");
        WritePdb(stagingDirectory, "win-x86", "PresentationCore.pdb");

        var nuspecPath = NuGetPackageService.GenerateSymbolNuspec(stagingDirectory, "1.2.3");
        var document = XDocument.Load(nuspecPath);
        XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        var targets = document.Descendants(ns + "file")
            .Select(element => element.Attribute("target")?.Value)
            .ToArray();

        Assert.Equal(
            [
                @"runtimes\win-x64\lib\net8.0\PresentationCore.pdb",
                @"runtimes\win-x64\lib\net8.0\PresentationFramework.pdb",
                @"runtimes\win-x86\lib\net8.0\PresentationCore.pdb",
            ],
            targets);
    }

    [Fact]
    public void GenerateSymbolNuspecDeclaresSymbolsPackageType()
    {
        var stagingDirectory = CreateStagingDirectory();
        WritePdb(stagingDirectory, "win-x64", "PresentationCore.pdb");

        var nuspecPath = NuGetPackageService.GenerateSymbolNuspec(stagingDirectory, "1.2.3");
        var document = XDocument.Load(nuspecPath);
        XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        var packageType = document.Descendants(ns + "packageType").Single();

        Assert.Equal("SymbolsPackage", packageType.Attribute("name")?.Value);
    }

    [Fact]
    public void GenerateSymbolNuspecThrowsWhenNoPdbFilesExist()
    {
        var stagingDirectory = CreateStagingDirectory();

        Assert.Throws<InvalidOperationException>(() =>
            NuGetPackageService.GenerateSymbolNuspec(stagingDirectory, "1.2.3"));
    }

    private static string CreateStagingDirectory()
    {
        var stagingDirectory = Path.Join(Path.GetTempPath(), $"builder-symbol-tests-{Guid.NewGuid():N}");
        foreach (var rid in new[] { "win-x64", "win-x86" })
        {
            Directory.CreateDirectory(Path.Join(stagingDirectory, "runtimes", rid, "lib", "net8.0"));
        }

        return stagingDirectory;
    }

    private static void WritePdb(string stagingDirectory, string rid, string fileName) =>
        File.WriteAllText(Path.Join(stagingDirectory, "runtimes", rid, "lib", "net8.0", fileName), "pdb");
}

using System.IO.Compression;
using System.Xml.Linq;
using WpfReorganize.Builder;

namespace WpfReorganize.Builder.Tests;

public sealed class NuGetPackageServiceTests
{
    [Fact]
    public void CopyIjwHostFromPackageCopiesFileToNativeAndRuntimeLibDirectories()
    {
        var packageRoot = Path.Join(Path.GetTempPath(), $"builder-host-package-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Join(packageRoot, "runtimes", "win-x64", "native");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Join(sourceDirectory, "ijwhost.dll"), "ijw-host");
        var nativeDirectory = Path.Join(Path.GetTempPath(), $"builder-native-{Guid.NewGuid():N}");
        var runtimeLibDirectory = Path.Join(Path.GetTempPath(), $"builder-runtime-lib-{Guid.NewGuid():N}");

        NuGetPackageService.CopyIjwHostFromPackage(
            new Dictionary<string, string> { ["host-win-x64"] = packageRoot },
            "win-x64",
            nativeDirectory,
            runtimeLibDirectory);

        Assert.Equal(
            ("ijw-host", "ijw-host"),
            (File.ReadAllText(Path.Join(nativeDirectory, "ijwhost.dll")),
             File.ReadAllText(Path.Join(runtimeLibDirectory, "ijwhost.dll"))));
    }

    [Fact]
    public void GenerateNuspecIncludesRepositoryReadme()
    {
        var stagingDirectory = CreateStagingDirectory();
        foreach (var rid in new[] { "win-x64", "win-x86" })
        {
            Directory.CreateDirectory(Path.Join(stagingDirectory, "runtimes", rid, "native"));
        }

        var readmePath = Path.Join(Path.GetTempPath(), $"builder-readme-{Guid.NewGuid():N}.md");
        File.WriteAllText(readmePath, "# Package README");

        var nuspecPath = NuGetPackageService.GenerateNuspec(stagingDirectory, "1.2.3", [], readmePath);
        var document = XDocument.Load(nuspecPath);
        XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        var readme = document.Descendants(ns + "readme").Single().Value;
        var readmeTarget = document.Descendants(ns + "file")
            .Single(element => element.Attribute("src")?.Value == "README.md")
            .Attribute("target")?.Value;
        var packagedContent = File.ReadAllText(Path.Join(stagingDirectory, "README.md"));

        Assert.Equal(("README.md", "README.md", "# Package README"), (readme, readmeTarget, packagedContent));
    }

    [Fact]
    public void GenerateNuspecIncludesBuildTransitivePropsAndTargets()
    {
        var stagingDirectory = CreateStagingDirectory();
        foreach (var rid in new[] { "win-x64", "win-x86" })
        {
            Directory.CreateDirectory(Path.Join(stagingDirectory, "runtimes", rid, "native"));
        }

        var readmePath = Path.Join(Path.GetTempPath(), $"builder-readme-{Guid.NewGuid():N}.md");
        File.WriteAllText(readmePath, "# Package README");

        var nuspecPath = NuGetPackageService.GenerateNuspec(stagingDirectory, "1.2.3", [], readmePath);
        var document = XDocument.Load(nuspecPath);
        XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        var buildTransitiveFiles = document.Descendants(ns + "file")
            .Select(element => element.Attribute("src")?.Value)
            .Where(value => value?.StartsWith("buildTransitive\\", StringComparison.Ordinal) == true)
            .ToList();

        Assert.Equal(
            [
                $"buildTransitive\\{PackageMetadata.Id}.props",
                $"buildTransitive\\{PackageMetadata.Id}.targets"
            ],
            buildTransitiveFiles);
    }

    [Fact]
    public void GenerateBuildTransitiveTargetsInfersRuntimeIdentifierFromPlatformTarget()
    {
        var stagingDirectory = CreateStagingDirectory();

        NuGetPackageService.GenerateBuildTransitiveTargets(stagingDirectory);
        var targetsContent = File.ReadAllText(
            Path.Join(stagingDirectory, "buildTransitive", $"{PackageMetadata.Id}.targets"));

        Assert.Contains(
            "Condition=\"'$(_DotNetCampusWpfRuntimeIdentifier)' == '' And ('$(PlatformTarget)' == 'x64' Or '$(Platform)' == 'x64')\">win-x64",
            targetsContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateBuildTransitiveTargetsCopiesNativeAssetsForInferredRuntimeIdentifier()
    {
        var stagingDirectory = CreateStagingDirectory();

        NuGetPackageService.GenerateBuildTransitiveTargets(stagingDirectory);
        var targetsContent = File.ReadAllText(
            Path.Join(stagingDirectory, "buildTransitive", $"{PackageMetadata.Id}.targets"));

        Assert.Contains(
            @"runtimes\$(_DotNetCampusWpfRuntimeIdentifier)\native\*.dll",
            targetsContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateBuildTransitiveTargetsSelectsSingleIjwHostPublishAsset()
    {
        var stagingDirectory = CreateStagingDirectory();

        NuGetPackageService.GenerateBuildTransitiveTargets(stagingDirectory);
        var targetsContent = File.ReadAllText(
            Path.Join(stagingDirectory, "buildTransitive", $"{PackageMetadata.Id}.targets"));

        Assert.Contains(
            "Condition=\"'%(ResolvedFileToPublish.Filename)%(ResolvedFileToPublish.Extension)' == 'ijwhost.dll'\"",
            targetsContent,
            StringComparison.Ordinal);
        Assert.Contains(
            @"<ResolvedFileToPublish Remove=""@(_DotNetCampusIjwHostPublishAsset)"" />",
            targetsContent,
            StringComparison.Ordinal);
        Assert.Contains(
            @"runtimes\$(_DotNetCampusWpfRuntimeIdentifier)\lib\net8.0\ijwhost.dll",
            targetsContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateSymbolNuspecIncludesMultiplePdbFilesWithRuntimePaths()
    {
        var stagingDirectory = CreateStagingDirectory();
        WritePortablePdb(stagingDirectory, "win-x64", "PresentationCore.pdb");
        WritePortablePdb(stagingDirectory, "win-x64", "PresentationFramework.pdb");
        WritePortablePdb(stagingDirectory, "win-x86", "PresentationCore.pdb");

        var nuspecPath = NuGetPackageService.GenerateSymbolNuspec(stagingDirectory, "1.2.3");
        var document = XDocument.Load(nuspecPath);
        XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        var targets = document.Descendants(ns + "file")
            .Select(element => element.Attribute("target")?.Value
                ?? throw new InvalidDataException("Symbol file target is missing"))
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
        WritePortablePdb(stagingDirectory, "win-x64", "PresentationCore.pdb");

        var nuspecPath = NuGetPackageService.GenerateSymbolNuspec(stagingDirectory, "1.2.3");
        var document = XDocument.Load(nuspecPath);
        XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        var packageType = document.Descendants(ns + "packageType").Single();

        Assert.Equal("SymbolsPackage", packageType.Attribute("name")?.Value);
    }

    [Fact]
    public void GenerateSymbolNuspecExcludesNonPortablePdbFiles()
    {
        var stagingDirectory = CreateStagingDirectory();
        WritePortablePdb(stagingDirectory, "win-x64", "Portable.pdb");
        WriteWindowsPdb(stagingDirectory, "win-x64", "Windows.pdb");

        var nuspecPath = NuGetPackageService.GenerateSymbolNuspec(stagingDirectory, "1.2.3");
        var document = XDocument.Load(nuspecPath);
        XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        var targets = document.Descendants(ns + "file")
            .Select(element => element.Attribute("target")?.Value
                ?? throw new InvalidDataException("Symbol file target is missing"))
            .ToArray();

        Assert.Equal([@"runtimes\win-x64\lib\net8.0\Portable.pdb"], targets);
    }

    [Fact]
    public void GenerateSymbolNuspecThrowsWhenNoPortablePdbFilesExist()
    {
        var stagingDirectory = CreateStagingDirectory();
        WriteWindowsPdb(stagingDirectory, "win-x64", "Windows.pdb");

        Assert.Throws<InvalidOperationException>(() =>
            NuGetPackageService.GenerateSymbolNuspec(stagingDirectory, "1.2.3"));
    }

    [Fact]
    public void CreateAllSymbolsArchiveIncludesPortableAndNonPortablePdbFiles()
    {
        var stagingDirectory = CreateStagingDirectory();
        WritePortablePdb(stagingDirectory, "win-x64", "PresentationCore.pdb");
        WriteWindowsPdb(stagingDirectory, "win-x86", "PresentationCore.pdb");
        var outputDirectory = Path.Join(Path.GetTempPath(), $"builder-symbol-output-{Guid.NewGuid():N}");

        var archivePath = NuGetPackageService.CreateAllSymbolsArchive(stagingDirectory, "1.2.3", outputDirectory);
        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive.Entries.Select(entry => entry.FullName).ToArray();

        Assert.Equal(
            [
                "runtimes/win-x64/lib/net8.0/PresentationCore.pdb",
                "runtimes/win-x86/lib/net8.0/PresentationCore.pdb",
            ],
            entries);
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

    private static void WritePortablePdb(string stagingDirectory, string rid, string fileName) =>
        File.WriteAllBytes(
            Path.Join(stagingDirectory, "runtimes", rid, "lib", "net8.0", fileName),
            "BSJBportable"u8.ToArray());

    private static void WriteWindowsPdb(string stagingDirectory, string rid, string fileName) =>
        File.WriteAllBytes(
            Path.Join(stagingDirectory, "runtimes", rid, "lib", "net8.0", fileName),
            "Microsoft C/C++ MSF 7.00"u8.ToArray());
}

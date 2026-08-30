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
    public void GenerateNuspecIncludesPresentationBuildTaskAssets()
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
        var presentationBuildTaskAssets = document.Descendants(ns + "file")
            .Select(element => element.Attribute("src")?.Value)
            .Where(value => value?.StartsWith("buildTransitive\\", StringComparison.Ordinal) == true
                || value?.StartsWith("tools\\net8.0\\", StringComparison.Ordinal) == true)
            .ToList();

        Assert.Equal(
            [
                $"buildTransitive\\{PackageMetadata.Id}.props",
                $"buildTransitive\\{PackageMetadata.Id}.targets",
                "tools\\net8.0\\PresentationBuildTasks.dll",
                "tools\\net8.0\\PresentationBuildTasks.deps.json",
                "tools\\net8.0\\System.Reflection.MetadataLoadContext.dll"
            ],
            presentationBuildTaskAssets);
    }

    [Fact]
    public void GenerateNuspecUsesDirectoriesForPresentationBuildTaskTargets()
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
        var targets = document.Descendants(ns + "file")
            .Where(element => element.Attribute("src")?.Value.StartsWith("buildTransitive\\", StringComparison.Ordinal) == true
                || element.Attribute("src")?.Value.StartsWith("tools\\net8.0\\", StringComparison.Ordinal) == true)
            .Select(element => element.Attribute("target")?.Value)
            .ToArray();

        Assert.Equal(["buildTransitive", "buildTransitive", "tools\\net8.0", "tools\\net8.0", "tools\\net8.0"], targets);
    }

    [Fact]
    public void PackNuGetIncludesPresentationBuildTasksAtExpectedPath()
    {
        var stagingDirectory = CreateStagingDirectory();
        foreach (var rid in new[] { "win-x64", "win-x86" })
        {
            Directory.CreateDirectory(Path.Join(stagingDirectory, "runtimes", rid, "native"));
        }

        var toolsDirectory = Path.Join(stagingDirectory, "tools", "net8.0");
        Directory.CreateDirectory(toolsDirectory);
        File.WriteAllText(Path.Join(toolsDirectory, "PresentationBuildTasks.dll"), "build-tasks");
        File.WriteAllText(Path.Join(toolsDirectory, "PresentationBuildTasks.deps.json"), "{}");
        File.WriteAllText(Path.Join(toolsDirectory, "System.Reflection.MetadataLoadContext.dll"), "metadata-load-context");
        NuGetPackageService.GenerateBuildTransitiveFiles(stagingDirectory);

        var readmePath = Path.Join(Path.GetTempPath(), $"builder-readme-{Guid.NewGuid():N}.md");
        File.WriteAllText(readmePath, "# Package README");
        var nuspecPath = NuGetPackageService.GenerateNuspec(stagingDirectory, "1.2.3", [], readmePath);
        var outputDirectory = Path.Join(Path.GetTempPath(), $"builder-package-output-{Guid.NewGuid():N}");

        var packagePath = NuGetPackageService.PackNuGet(nuspecPath, outputDirectory);
        using var archive = ZipFile.OpenRead(packagePath);
        var entries = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("tools/net8.0/PresentationBuildTasks.dll", entries);
    }

    [Fact]
    public void GenerateBuildTransitivePropsPreservesSdkFrameworkReferences()
    {
        var stagingDirectory = CreateStagingDirectory();

        NuGetPackageService.GenerateBuildTransitiveFiles(stagingDirectory);
        var propsContent = File.ReadAllText(
            Path.Join(stagingDirectory, "buildTransitive", $"{PackageMetadata.Id}.props"));

        Assert.DoesNotContain("DisableImplicitFrameworkReferences", propsContent, StringComparison.Ordinal);
        Assert.DoesNotContain("FrameworkReference", propsContent, StringComparison.Ordinal);
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
    public void GenerateBuildTransitiveTargetsInfersRuntimeIdentifierForAnyCpuProjects()
    {
        var stagingDirectory = CreateStagingDirectory();

        NuGetPackageService.GenerateBuildTransitiveTargets(stagingDirectory);
        var targetsContent = File.ReadAllText(
            Path.Join(stagingDirectory, "buildTransitive", $"{PackageMetadata.Id}.targets"));

        Assert.Contains(
            "Condition=\"'$(_DotNetCampusWpfRuntimeIdentifier)' == '' And '$(NETCoreSdkRuntimeIdentifier)' == 'win-x64'\">win-x64",
            targetsContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateBuildTransitiveTargetsUsesX86RuntimeWhenPrefer32BitIsEnabled()
    {
        var stagingDirectory = CreateStagingDirectory();

        NuGetPackageService.GenerateBuildTransitiveTargets(stagingDirectory);
        var targetsContent = File.ReadAllText(
            Path.Join(stagingDirectory, "buildTransitive", $"{PackageMetadata.Id}.targets"));

        Assert.Contains(
            "Or '$(Prefer32Bit)' == 'true')\">win-x86",
            targetsContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateBuildTransitiveTargetsRunsBeforeWpfCompilationStages()
    {
        var stagingDirectory = CreateStagingDirectory();

        NuGetPackageService.GenerateBuildTransitiveTargets(stagingDirectory);
        var targetsContent = File.ReadAllText(
            Path.Join(stagingDirectory, "buildTransitive", $"{PackageMetadata.Id}.targets"));

        Assert.Contains(
            "BeforeTargets=\"MarkupCompilePass1;GenerateTemporaryTargetAssembly;CoreCompile\"",
            targetsContent,
            StringComparison.Ordinal);
        Assert.Contains(
            @"<_DotNetCampusWpfReplacementDll Include=""$(MSBuildThisFileDirectory)..\runtimes\win-x64\lib\net8.0\*.dll"" />",
            targetsContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateBuildTransitiveTargetsReplacesEveryPackagedWpfReferenceAsync()
    {
        var stagingDirectory = CreateStagingDirectory();
        var referenceDirectory = Path.Join(stagingDirectory, "ref", "net8.0");
        Directory.CreateDirectory(referenceDirectory);
        File.WriteAllText(Path.Join(referenceDirectory, "WindowsBase.dll"), "package-windows-base");
        File.WriteAllText(Path.Join(referenceDirectory, "PresentationFramework.dll"), "package-presentation-framework");
        var runtimeDirectory = Path.Join(stagingDirectory, "runtimes", "win-x64", "lib", "net8.0");
        Directory.CreateDirectory(runtimeDirectory);
        File.WriteAllText(Path.Join(runtimeDirectory, "WindowsFormsIntegration.dll"), "package-windows-forms-integration");
        NuGetPackageService.GenerateBuildTransitiveTargets(stagingDirectory);

        var outputPath = Path.Join(stagingDirectory, "references.txt");
        var projectPath = Path.Join(stagingDirectory, "ReferenceReplacement.proj");
        var targetsPath = Path.Join(stagingDirectory, "buildTransitive", $"{PackageMetadata.Id}.targets");
        var projectContent = $$"""
            <Project>
              <Import Project="{{targetsPath}}" />
              <Target Name="ResolveReferences">
                <ItemGroup>
                  <ReferencePath Include="inbox\WindowsBase.dll">
                    <Version>9.0.0.0</Version>
                  </ReferencePath>
                  <ReferencePath Include="inbox\PresentationFramework.dll">
                    <Version>9.0.0.0</Version>
                    <FrameworkReferenceName>Microsoft.WindowsDesktop.App</FrameworkReferenceName>
                  </ReferencePath>
                  <ReferencePath Include="inbox\WindowsFormsIntegration.dll">
                    <Version>9.0.0.0</Version>
                    <FrameworkReferenceName>Microsoft.WindowsDesktop.App</FrameworkReferenceName>
                  </ReferencePath>
                  <ReferencePath Include="inbox\System.CodeDom.dll">
                    <Version>9.0.0.0</Version>
                    <FrameworkReferenceName>Microsoft.WindowsDesktop.App</FrameworkReferenceName>
                  </ReferencePath>
                </ItemGroup>
              </Target>
              <Target Name="Validate" DependsOnTargets="ResolveReferences;RemoveInboxWpfReferencesForDotNetCampusWpfLib">
                <WriteLinesToFile File="{{outputPath}}" Lines="@(ReferencePath->'%(Identity)|%(Filename)|%(Version)')" Overwrite="true" />
              </Target>
            </Project>
            """;
        File.WriteAllText(projectPath, projectContent);

        var result = await ProcessRunner.RunAsync(new ProcessRunOptions(
            "dotnet",
            stagingDirectory,
            "msbuild",
            projectPath,
            "-target:Validate",
            "-nologo",
            "-verbosity:quiet"));
        Assert.True(
            result.ExitCode == 0,
            $"MSBuild failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}{result.StandardError}");
        var references = File.ReadAllLines(outputPath);

        var normalizedReferences = references
            .Select(reference => reference.Replace(@"\\", @"\", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(3, normalizedReferences.Length);
        Assert.Contains(normalizedReferences, reference => reference.Contains(@"\ref\net8.0\WindowsBase.dll|WindowsBase|", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(normalizedReferences, reference => reference.Contains(@"\ref\net8.0\PresentationFramework.dll|PresentationFramework|", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(normalizedReferences, reference => reference.Contains(@"inbox\System.CodeDom.dll|System.CodeDom|9.0.0.0", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(normalizedReferences, reference => reference.Contains(@"inbox\WindowsFormsIntegration.dll", StringComparison.OrdinalIgnoreCase));
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
    public void GenerateBuildTransitiveTargetsRegistersDirectWriteForwarderAsPrivateReference()
    {
        var stagingDirectory = CreateStagingDirectory();

        NuGetPackageService.GenerateBuildTransitiveTargets(stagingDirectory);
        var targetsContent = File.ReadAllText(
            Path.Join(stagingDirectory, "buildTransitive", $"{PackageMetadata.Id}.targets"));

        Assert.Contains(
            @"<Reference Include=""DirectWriteForwarder"">",
            targetsContent,
            StringComparison.Ordinal);
        Assert.Contains(
            @"<HintPath>$(MSBuildThisFileDirectory)..\runtimes\$(_DotNetCampusWpfRuntimeIdentifier)\lib\net8.0\DirectWriteForwarder.dll</HintPath>",
            targetsContent,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Private>true</Private>",
            targetsContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateBuildTransitiveTargetsPreservesWindowsDesktopFrameworkReferences()
    {
        var stagingDirectory = CreateStagingDirectory();

        NuGetPackageService.GenerateBuildTransitiveTargets(stagingDirectory);
        var targetsContent = File.ReadAllText(
            Path.Join(stagingDirectory, "buildTransitive", $"{PackageMetadata.Id}.targets"));

        Assert.DoesNotContain("<FrameworkReference Remove=", targetsContent, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateBuildTransitiveTargetsRegistersManagedRuntimeAssembliesForDepsFile()
    {
        var stagingDirectory = CreateStagingDirectory();

        NuGetPackageService.GenerateBuildTransitiveTargets(stagingDirectory);
        var targetsContent = File.ReadAllText(
            Path.Join(stagingDirectory, "buildTransitive", $"{PackageMetadata.Id}.targets"));

        Assert.Contains("<ReferenceDependencyPaths Include=\"@(_DotNetCampusWpfManagedRuntimeDll)\"", targetsContent, StringComparison.Ordinal);
        Assert.Contains("IncludeRuntimeDependency=\"true\"", targetsContent, StringComparison.Ordinal);
        Assert.Contains("<ReferenceCopyLocalPaths Include=\"@(_DotNetCampusWpfManagedRuntimeDll)\"", targetsContent, StringComparison.Ordinal);
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

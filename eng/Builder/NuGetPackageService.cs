using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace WpfReorganize.Builder;

internal static class NuGetPackageService
{
    public static Dictionary<string, string> ReadPackagePaths(string builderOutputDir)
    {
        var pathsFile = Path.Join(builderOutputDir, "PackagePaths.txt");
        if (!File.Exists(pathsFile))
        {
            throw new InvalidOperationException($"Package paths file not found: {pathsFile}; build the Builder project first to resolve NuGet package paths");
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(pathsFile))
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                result[parts[0].Trim()] = parts[1].Trim();
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("Package paths file is empty; please check NuGet package references");
        }

        Log.Info($"  Package paths file: {pathsFile}");
        foreach (var (key, path) in result.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            Log.Info($"  Package path [{key}]: {path} (exists: {Directory.Exists(path)})");
        }

        return result;
    }

public static IReadOnlyList<PackageDependency> ReadRuntimePackageDependencies(string repoRoot) =>
    WpfRuntimeDefinition.ReadRuntimePackageDependencies(repoRoot);


public static void CopyNativeDllsFromPackage(Dictionary<string, string> packagePaths, string rid, string destDir)
{
    if (!packagePaths.TryGetValue(rid, out var packageRoot))
    {
        Log.Warn($"Package path not found for {rid}");
        return;
    }

    var sourceDir = Path.Join(packageRoot, "runtimes", rid, "native");

    if (!Directory.Exists(sourceDir))
    {
        Log.Warn($"Native source directory does not exist: {sourceDir}");
        return;
    }

    Directory.CreateDirectory(destDir);
    foreach (var file in Directory.GetFiles(sourceDir, "*.dll"))
    {
        var fileName = Path.GetFileName(file);
        var destPath = Path.Join(destDir, fileName);
        File.Copy(file, destPath, overwrite: true);
        Log.Info($"  runtimes/{rid}/native/{fileName}");
    }
}

public static void CopyIjwHostFromPackage(
    Dictionary<string, string> packagePaths,
    string rid,
    string nativeDestDir,
    string runtimeLibDestDir)
{
    var packageKey = $"host-{rid}";
    if (!packagePaths.TryGetValue(packageKey, out var packageRoot))
        throw new InvalidOperationException($".NET host package path '{packageKey}' not found. Available keys: {string.Join(", ", packagePaths.Keys.Order())}");

    RequirePackageDirectory(packageKey, packageRoot);
    var sourcePath = Path.Join(packageRoot, "runtimes", rid, "native", "ijwhost.dll");
    RequirePackageFile(sourcePath);

    Directory.CreateDirectory(nativeDestDir);
    File.Copy(sourcePath, Path.Join(nativeDestDir, "ijwhost.dll"), overwrite: true);
    Log.Info($"  runtimes/{rid}/native/ijwhost.dll");

    // Keep ijwhost.dll beside DirectWriteForwarder.dll because the Windows loader does not search
    // the sibling native asset directory when loading the C++/CLI assembly. See https://github.com/dotnet/runtime/issues/38231.
    Directory.CreateDirectory(runtimeLibDestDir);
    File.Copy(sourcePath, Path.Join(runtimeLibDestDir, "ijwhost.dll"), overwrite: true);
    Log.Info($"  runtimes/{rid}/lib/net8.0/ijwhost.dll");
}

public static string GenerateNuspec(
    string stagingDir,
    string version,
    IReadOnlyList<PackageDependency> runtimePackageDependencies,
    string readmePath)
{
    if (!File.Exists(readmePath))
    {
        throw new FileNotFoundException("Package README file was not found", readmePath);
    }

    const string packageReadmeFileName = "README.md";
    File.Copy(readmePath, Path.Join(stagingDir, packageReadmeFileName), overwrite: true);

    var referenceDir = Path.Join(stagingDir, "ref", "net8.0");
    var referenceFiles = Directory.Exists(referenceDir)
        ? Directory.GetFiles(referenceDir, "*.dll").Select(Path.GetFileName).OrderBy(x => x).ToList()
        : [];

    var dependencyGroups = new StringBuilder();
    foreach (var targetFramework in PackageMetadata.TargetFrameworks)
    {
        dependencyGroups.AppendLine($"      <group targetFramework=\"{targetFramework}\">");
        foreach (var dependency in runtimePackageDependencies)
        {
            dependencyGroups.AppendLine($"        <dependency id=\"{dependency.Id}\" version=\"[{dependency.Version}, )\" />");
        }
        dependencyGroups.AppendLine("      </group>");
    }

    var files = new StringBuilder();
    foreach (var file in referenceFiles)
    {
        files.AppendLine($"    <file src=\"ref\\net8.0\\{file}\" target=\"ref\\net8.0\\{file}\" />");
    }

    foreach (var rid in new[] { "win-x64", "win-x86" })
    {
        var runtimeLibDir = Path.Join(stagingDir, "runtimes", rid, "lib", "net8.0");
        foreach (var file in Directory.GetFiles(runtimeLibDir, "*.dll").OrderBy(Path.GetFileName))
        {
            var fileName = Path.GetFileName(file);
            files.AppendLine($"    <file src=\"runtimes\\{rid}\\lib\\net8.0\\{fileName}\" target=\"runtimes\\{rid}\\lib\\net8.0\\{fileName}\" />");
        }

        var nativeDir = Path.Join(stagingDir, "runtimes", rid, "native");
        foreach (var file in Directory.GetFiles(nativeDir, "*.dll").OrderBy(Path.GetFileName))
        {
            var fileName = Path.GetFileName(file);
            files.AppendLine($"    <file src=\"runtimes\\{rid}\\native\\{fileName}\" target=\"runtimes\\{rid}\\native\\{fileName}\" />");
        }
    }

    files.AppendLine($"    <file src=\"buildTransitive\\{PackageMetadata.Id}.props\" target=\"buildTransitive\" />");
    files.AppendLine($"    <file src=\"buildTransitive\\{PackageMetadata.Id}.targets\" target=\"buildTransitive\" />");
    files.AppendLine("    <file src=\"tools\\net8.0\\PresentationBuildTasks.dll\" target=\"tools\\net8.0\" />");
    files.AppendLine("    <file src=\"tools\\net8.0\\PresentationBuildTasks.deps.json\" target=\"tools\\net8.0\" />");
    files.AppendLine("    <file src=\"tools\\net8.0\\System.Reflection.MetadataLoadContext.dll\" target=\"tools\\net8.0\" />");
    files.AppendLine($"    <file src=\"{packageReadmeFileName}\" target=\"{packageReadmeFileName}\" />");

    var nuspecContent = $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
          <metadata>
            <id>{{PackageMetadata.Id}}</id>
            <version>{{version}}</version>
            <authors>WpfLab</authors>
            <description>Custom-built WPF managed assemblies and native runtimes.</description>
            <copyright>WpfLab</copyright>
            <license type="expression">MIT</license>
            <projectUrl>{{PackageMetadata.ProjectUrl}}</projectUrl>
            <readme>{{packageReadmeFileName}}</readme>
            <tags>WPF WindowsDesktop</tags>
            <dependencies>
        {{dependencyGroups}}    </dependencies>
          </metadata>
          <files>
        {{files}}  </files>
        </package>
        """ + Environment.NewLine;
    var nuspecPath = Path.Join(stagingDir, $"{PackageMetadata.Id}.nuspec");
    File.WriteAllText(nuspecPath, nuspecContent);
    Log.Info($"  .nuspec generated: {nuspecPath}");
    return nuspecPath;
}

public static string GenerateSymbolNuspec(string stagingDir, string version)
{
    var files = new StringBuilder();
    foreach (var rid in new[] { "win-x64", "win-x86" })
    {
        var runtimeLibDir = Path.Join(stagingDir, "runtimes", rid, "lib", "net8.0");
        foreach (var file in Directory.GetFiles(runtimeLibDir, "*.pdb").OrderBy(Path.GetFileName))
        {
            var fileName = Path.GetFileName(file);
            if (!IsPortablePdb(file))
            {
                Log.Info($"  Excluding non-portable PDB from symbol package: runtimes/{rid}/lib/net8.0/{fileName}");
                continue;
            }

            files.AppendLine($"    <file src=\"runtimes\\{rid}\\lib\\net8.0\\{fileName}\" target=\"runtimes\\{rid}\\lib\\net8.0\\{fileName}\" />");
        }
    }

    if (files.Length == 0)
    {
        throw new InvalidOperationException("No portable PDB files were found for the symbol package");
    }

    var nuspecContent = $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
          <metadata>
            <id>{{PackageMetadata.Id}}</id>
            <version>{{version}}</version>
            <authors>WpfLab</authors>
            <description>Portable symbols for {{PackageMetadata.Id}}.</description>
            <copyright>WpfLab</copyright>
            <license type="expression">MIT</license>
            <projectUrl>{{PackageMetadata.ProjectUrl}}</projectUrl>
            <tags>WPF WindowsDesktop symbols</tags>
            <packageTypes>
              <packageType name="SymbolsPackage" />
            </packageTypes>
          </metadata>
          <files>
        {{files}}  </files>
        </package>
        """ + Environment.NewLine;
    var nuspecPath = Path.Join(stagingDir, $"{PackageMetadata.Id}.symbols.nuspec");
    File.WriteAllText(nuspecPath, nuspecContent);
    Log.Info($"  Symbol .nuspec generated: {nuspecPath}");
    return nuspecPath;
}

public static void GenerateBuildTransitiveFiles(string stagingDir)
{
    var buildTransitiveDir = Path.Join(stagingDir, "buildTransitive");
    Directory.CreateDirectory(buildTransitiveDir);
    var propsPath = Path.Join(buildTransitiveDir, $"{PackageMetadata.Id}.props");
    var propsContent = """
        <Project>
          <PropertyGroup Condition="'$(MSBuildRuntimeType)' == 'Core'">
            <_PresentationBuildTasksAssembly>$(MSBuildThisFileDirectory)..\tools\net8.0\PresentationBuildTasks.dll</_PresentationBuildTasksAssembly>
          </PropertyGroup>
        </Project>
        """;
    File.WriteAllText(propsPath, propsContent);
    Log.Info($"  buildTransitive/{PackageMetadata.Id}.props");

    GenerateBuildTransitiveTargets(stagingDir);
}

public static void GenerateBuildTransitiveTargets(string stagingDir)
{
    var targetsDir = Path.Join(stagingDir, "buildTransitive");
    Directory.CreateDirectory(targetsDir);
    var targetsPath = Path.Join(targetsDir, $"{PackageMetadata.Id}.targets");
    var content = """
        <Project>
          <PropertyGroup>
            <_DotNetCampusWpfRuntimeIdentifier Condition="'$(RuntimeIdentifier)' == 'win-x86' Or '$(RuntimeIdentifier)' == 'win-x64'">$(RuntimeIdentifier)</_DotNetCampusWpfRuntimeIdentifier>
            <_DotNetCampusWpfRuntimeIdentifier Condition="'$(_DotNetCampusWpfRuntimeIdentifier)' == '' And ('$(PlatformTarget)' == 'x64' Or '$(Platform)' == 'x64')">win-x64</_DotNetCampusWpfRuntimeIdentifier>
            <_DotNetCampusWpfRuntimeIdentifier Condition="'$(_DotNetCampusWpfRuntimeIdentifier)' == '' And ('$(PlatformTarget)' == 'x86' Or '$(Platform)' == 'x86' Or '$(Platform)' == 'Win32')">win-x86</_DotNetCampusWpfRuntimeIdentifier>
          </PropertyGroup>

          <ItemGroup>
            <FrameworkReference Remove="Microsoft.WindowsDesktop.App.WPF" />
          </ItemGroup>

          <ItemGroup Condition="'$(_DotNetCampusWpfRuntimeIdentifier)' != ''">
            <_DotNetCampusWpfRuntimeDll Include="$(MSBuildThisFileDirectory)..\runtimes\$(_DotNetCampusWpfRuntimeIdentifier)\lib\net8.0\*.dll" />
            <_DotNetCampusWpfRuntimeDll Include="$(MSBuildThisFileDirectory)..\runtimes\$(_DotNetCampusWpfRuntimeIdentifier)\native\*.dll" />
          </ItemGroup>

          <ItemGroup>
            <_DotNetCampusWpfReferenceDll Include="$(MSBuildThisFileDirectory)..\ref\net8.0\*.dll" />
          </ItemGroup>

          <Target Name="RemoveInboxWpfReferencesForDotNetCampusWpfLib"
                  AfterTargets="ResolveReferences"
                  BeforeTargets="MarkupCompilePass1;GenerateTemporaryTargetAssembly;CoreCompile">
            <Message Importance="high"
                     Condition="'$(WpfRuntimeReferenceDiagnostics)' == 'true'"
                     Text="[WpfRuntime refs before] Project=$(MSBuildProjectFullPath); TargetFramework=$(TargetFramework); RuntimeIdentifier=$(RuntimeIdentifier); FrameworkReference=@(FrameworkReference->'%(Identity)', ', '); ReferencePath=@(ReferencePath->'%(Filename)|%(Version)|%(FrameworkReferenceName)|%(Identity)', ' || ')" />
            <ItemGroup>
              <ReferencePath Remove="@(_DotNetCampusWpfReferenceDll)"
                             MatchOnMetadata="Filename" />
              <ReferencePathWithRefAssemblies Remove="@(_DotNetCampusWpfReferenceDll)"
                                               MatchOnMetadata="Filename" />
              <ReferencePath Include="@(_DotNetCampusWpfReferenceDll)" />
              <ReferencePathWithRefAssemblies Include="@(_DotNetCampusWpfReferenceDll)" />
            </ItemGroup>
            <Message Importance="high"
                     Condition="'$(WpfRuntimeReferenceDiagnostics)' == 'true'"
                     Text="[WpfRuntime refs after] Project=$(MSBuildProjectFullPath); TargetFramework=$(TargetFramework); RuntimeIdentifier=$(RuntimeIdentifier); ReferencePath=@(ReferencePath->'%(Filename)|%(Version)|%(FrameworkReferenceName)|%(Identity)', ' || ')" />
          </Target>

          <Target Name="SelectDotNetCampusIjwHostPublishAsset"
                  BeforeTargets="_HandleFileConflictsForPublish"
                  Condition="'$(_DotNetCampusWpfRuntimeIdentifier)' != ''">
            <ItemGroup>
              <_DotNetCampusIjwHostPublishAsset Include="@(ResolvedFileToPublish)"
                                                Condition="'%(ResolvedFileToPublish.Filename)%(ResolvedFileToPublish.Extension)' == 'ijwhost.dll'" />
              <ResolvedFileToPublish Remove="@(_DotNetCampusIjwHostPublishAsset)" />
              <ResolvedFileToPublish Include="$(MSBuildThisFileDirectory)..\runtimes\$(_DotNetCampusWpfRuntimeIdentifier)\lib\net8.0\ijwhost.dll">
                <RelativePath>ijwhost.dll</RelativePath>
                <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
              </ResolvedFileToPublish>
            </ItemGroup>
          </Target>

          <Target Name="CopyDotNetCampusWpfRuntimeDllsToOutput"
                  AfterTargets="Build"
                  Condition="'@(_DotNetCampusWpfRuntimeDll)' != ''">
            <Copy SourceFiles="@(_DotNetCampusWpfRuntimeDll)"
                  DestinationFolder="$(TargetDir)"
                  SkipUnchangedFiles="true" />
          </Target>

          <Target Name="CopyDotNetCampusWpfRuntimeDllsToPublish"
                  AfterTargets="Publish"
                  Condition="'@(_DotNetCampusWpfRuntimeDll)' != '' And '$(PublishDir)' != ''">
            <Copy SourceFiles="@(_DotNetCampusWpfRuntimeDll)"
                  DestinationFolder="$(PublishDir)"
                  SkipUnchangedFiles="true" />
          </Target>
        </Project>
        """;
    File.WriteAllText(targetsPath, content);
    Log.Info($"  buildTransitive/{PackageMetadata.Id}.targets");
}

public static void CopyPresentationBuildTasks(string sourceDir, string stagingDir)
{
    var toolsDir = Path.Join(stagingDir, "tools", "net8.0");
    Directory.CreateDirectory(toolsDir);
    foreach (var fileName in new[]
    {
        "PresentationBuildTasks.dll",
        "PresentationBuildTasks.deps.json",
        "System.Reflection.MetadataLoadContext.dll",
    })
    {
        var sourcePath = Path.Join(sourceDir, fileName);
        RequirePackageFile(sourcePath);
        File.Copy(sourcePath, Path.Join(toolsDir, fileName), overwrite: true);
        Log.Info($"  tools/net8.0/{fileName}");
    }
}

public static void ValidatePackageAssets(string stagingDir)
{
    RequirePackageFile(Path.Join(stagingDir, "buildTransitive", $"{PackageMetadata.Id}.props"));
    RequirePackageFile(Path.Join(stagingDir, "buildTransitive", $"{PackageMetadata.Id}.targets"));
    RequirePackageFile(Path.Join(stagingDir, "tools", "net8.0", "PresentationBuildTasks.dll"));
    RequirePackageFile(Path.Join(stagingDir, "tools", "net8.0", "PresentationBuildTasks.deps.json"));
    RequirePackageFile(Path.Join(stagingDir, "tools", "net8.0", "System.Reflection.MetadataLoadContext.dll"));

    var requiredReferenceAssemblies = new[]
    {
        "WindowsBase.dll",
        "PresentationCore.dll",
        "PresentationFramework.dll",
    };
    var requiredRuntimeAssemblies = new[]
    {
        "WindowsBase.dll",
        "PresentationCore.dll",
        "PresentationFramework.dll",
        "DirectWriteForwarder.dll",
        "ijwhost.dll",
    };
    var requiredNativeAssemblies = new[]
    {
        "ijwhost.dll",
        "PenImc_cor3.dll",
        "PresentationNative_cor3.dll",
        "wpfgfx_cor3.dll",
    };

    foreach (var fileName in requiredReferenceAssemblies)
    {
        RequirePackageFile(Path.Join(stagingDir, "ref", "net8.0", fileName));
    }

    foreach (var rid in new[] { "win-x64", "win-x86" })
    {
        foreach (var fileName in requiredRuntimeAssemblies)
        {
            RequirePackageFile(Path.Join(stagingDir, "runtimes", rid, "lib", "net8.0", fileName));
        }

        foreach (var fileName in requiredNativeAssemblies)
        {
            RequirePackageFile(Path.Join(stagingDir, "runtimes", rid, "native", fileName));
        }
    }

    Log.Info("  Package asset validation passed for win-x64 and win-x86");
}

private static void RequirePackageDirectory(string packageKey, string packageRoot)
{
    if (Directory.Exists(packageRoot))
    {
        return;
    }

    var parentDirectory = Path.GetDirectoryName(packageRoot);
    var availableEntries = parentDirectory is not null && Directory.Exists(parentDirectory)
        ? string.Join(", ", Directory.EnumerateFileSystemEntries(parentDirectory)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(20))
        : "<parent directory unavailable>";
    throw new InvalidOperationException($"NuGet package directory '{packageKey}' not found: {packageRoot}. Parent entries: {availableEntries}");
}

private static void RequirePackageFile(string path)
{
    if (!File.Exists(path))
        throw new InvalidOperationException($"Required package asset not found: {path}");
}

public static string PackNuGet(string nuspecPath, string outputDir)
{
    Directory.CreateDirectory(outputDir);
    var nupkgPath = PackNuspec(nuspecPath, outputDir);
    ValidatePackedPresentationBuildTaskAssets(nupkgPath);
    var fileInfo = new FileInfo(nupkgPath);
    Log.Info($"  .nupkg generated: {nupkgPath} ({fileInfo.Length / 1024.0:F1} KB)");
    return nupkgPath;
}

internal static void ValidatePackedPresentationBuildTaskAssets(string nupkgPath)
{
    using var archive = ZipFile.OpenRead(nupkgPath);
    var entries = archive.Entries
        .Select(entry => entry.FullName)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var requiredEntries = new[]
    {
        $"buildTransitive/{PackageMetadata.Id}.props",
        $"buildTransitive/{PackageMetadata.Id}.targets",
        "tools/net8.0/PresentationBuildTasks.dll",
        "tools/net8.0/PresentationBuildTasks.deps.json",
        "tools/net8.0/System.Reflection.MetadataLoadContext.dll",
    };

    foreach (var entry in requiredEntries)
    {
        if (!entries.Contains(entry))
            throw new InvalidOperationException($"Required packed NuGet asset not found: {entry}");
    }
}

public static string CreateAllSymbolsArchive(string buildOutputDir, string version, string outputDir)
{
    Directory.CreateDirectory(outputDir);
    var archivePath = Path.Join(outputDir, $"{PackageMetadata.Id}.{version}.symbols.zip");
    File.Delete(archivePath);

    var pdbFiles = Directory.GetFiles(buildOutputDir, "*.pdb", SearchOption.AllDirectories)
        .OrderBy(path => Path.GetRelativePath(buildOutputDir, path), StringComparer.OrdinalIgnoreCase)
        .ToArray();
    if (pdbFiles.Length == 0)
    {
        throw new InvalidOperationException("No PDB files were found for the all-symbols archive");
    }

    using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
    {
        foreach (var pdbPath in pdbFiles)
        {
            var entryName = Path.GetRelativePath(buildOutputDir, pdbPath).Replace('\\', '/');
            archive.CreateEntryFromFile(pdbPath, entryName, CompressionLevel.Optimal);
        }
    }

    var fileInfo = new FileInfo(archivePath);
    Log.Info($"  All-symbols archive generated: {archivePath} ({fileInfo.Length / 1024.0:F1} KB, {pdbFiles.Length} PDB files)");
    return archivePath;
}

public static string PackSymbolNuGet(string nuspecPath, string outputDir)
{
    Directory.CreateDirectory(outputDir);
    var symbolOutputDir = Path.Join(Path.GetTempPath(), "WpfBuilderSymbols", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(symbolOutputDir);

    try
    {
        var packedPath = PackNuspec(nuspecPath, symbolOutputDir);
        var snupkgPath = Path.Join(outputDir, $"{Path.GetFileNameWithoutExtension(packedPath)}.snupkg");
        File.Move(packedPath, snupkgPath, overwrite: true);
        var fileInfo = new FileInfo(snupkgPath);
        Log.Info($"  .snupkg generated: {snupkgPath} ({fileInfo.Length / 1024.0:F1} KB)");
        return snupkgPath;
    }
    finally
    {
        Directory.Delete(symbolOutputDir, recursive: true);
    }
}

private static bool IsPortablePdb(string path)
{
    Span<byte> signature = stackalloc byte[4];
    using var stream = File.OpenRead(path);
    return stream.Read(signature) == signature.Length
        && signature.SequenceEqual("BSJB"u8);
}

private static string PackNuspec(string nuspecPath, string outputDir)
{
    // Place _pack.csproj outside the repo tree (system temp directory)
    // to avoid inheriting Arcade SDK imports from the repo root Directory.Build.props
    var packDir = Path.Join(Path.GetTempPath(), "WpfBuilderPack", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(packDir);

    try
    {
        var nuspecRelativePath = Path.GetRelativePath(packDir, nuspecPath);
        var tempProj = Path.Join(packDir, "_pack.csproj");
        File.WriteAllText(tempProj, "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
            "  <PropertyGroup>\n" +
            "    <TargetFramework>net8.0</TargetFramework>\n" +
            $"    <NuspecFile>{nuspecRelativePath}</NuspecFile>\n" +
            "    <IsPackable>true</IsPackable>\n" +
            "    <NoDefaultExcludes>true</NoDefaultExcludes>\n" +
            "  </PropertyGroup>\n" +
            "</Project>\n");

        var result = ProcessRunner.Run("dotnet", $"pack \"{tempProj}\" --output \"{outputDir}\"", packDir);
        if (result.ExitCode != 0)
        {
            Log.Error($"Pack failed: {result.Output}");
            throw new InvalidOperationException("NuGet pack failed");
        }

        var nupkgFiles = Directory.GetFiles(outputDir, "*.nupkg");
        if (nupkgFiles.Length == 0)
        {
            throw new InvalidOperationException("No generated .nupkg file found");
        }

        return nupkgFiles.OrderByDescending(File.GetLastWriteTime).First();
    }
    finally
    {
        Directory.Delete(packDir, recursive: true);
    }
}
}

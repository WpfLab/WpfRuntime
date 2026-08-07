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

public static void CopyIjwHostFromPackage(Dictionary<string, string> packagePaths, string rid, string destDir)
{
    var packageKey = $"host-{rid}";
    if (!packagePaths.TryGetValue(packageKey, out var packageRoot))
        throw new InvalidOperationException($".NET host package path '{packageKey}' not found. Available keys: {string.Join(", ", packagePaths.Keys.Order())}");

    RequirePackageDirectory(packageKey, packageRoot);
    var sourcePath = Path.Join(packageRoot, "runtimes", rid, "native", "ijwhost.dll");
    RequirePackageFile(sourcePath);
    Directory.CreateDirectory(destDir);
    File.Copy(sourcePath, Path.Join(destDir, "ijwhost.dll"), overwrite: true);
    Log.Info($"  runtimes/{rid}/native/ijwhost.dll");
}

public static string GenerateNuspec(
    string stagingDir,
    string version,
    IReadOnlyList<PackageDependency> runtimePackageDependencies)
{
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

    files.AppendLine($"    <file src=\"buildTransitive\\{PackageMetadata.Id}.targets\" target=\"buildTransitive\\{PackageMetadata.Id}.targets\" />");

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
            files.AppendLine($"    <file src=\"runtimes\\{rid}\\lib\\net8.0\\{fileName}\" target=\"runtimes\\{rid}\\lib\\net8.0\\{fileName}\" />");
        }
    }

    if (files.Length == 0)
    {
        throw new InvalidOperationException("No PDB files were found for the symbol package");
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

public static void GenerateBuildTransitiveTargets(string stagingDir)
{
    var targetsDir = Path.Join(stagingDir, "buildTransitive");
    Directory.CreateDirectory(targetsDir);
    var targetsPath = Path.Join(targetsDir, $"{PackageMetadata.Id}.targets");
    var content = """
        <Project>
          <ItemGroup>
            <FrameworkReference Remove="Microsoft.WindowsDesktop.App.WPF" />
          </ItemGroup>

          <ItemGroup Condition="'$(RuntimeIdentifier)' == 'win-x86' Or '$(RuntimeIdentifier)' == 'win-x64'">
            <_DotNetCampusWpfRuntimeDll Include="$(MSBuildThisFileDirectory)..\runtimes\$(RuntimeIdentifier)\lib\net8.0\*.dll" />
            <_DotNetCampusWpfRuntimeDll Include="$(MSBuildThisFileDirectory)..\runtimes\$(RuntimeIdentifier)\native\*.dll" />
          </ItemGroup>

          <ItemGroup>
            <_DotNetCampusWpfReferenceDll Include="$(MSBuildThisFileDirectory)..\ref\net8.0\*.dll" />
          </ItemGroup>

          <Target Name="RemoveInboxWpfReferencesForDotNetCampusWpfLib"
                  AfterTargets="ResolveReferences">
            <ItemGroup>
              <ReferencePath Remove="@(ReferencePath)"
                             Condition="'%(ReferencePath.Filename)' == 'WindowsBase' Or '%(ReferencePath.Filename)' == 'PresentationCore' Or '%(ReferencePath.Filename)' == 'PresentationFramework' Or '%(ReferencePath.Filename)' == 'ReachFramework' Or '%(ReferencePath.Filename)' == 'System.Printing'" />
              <ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)"
                                               Condition="'%(ReferencePathWithRefAssemblies.Filename)' == 'WindowsBase' Or '%(ReferencePathWithRefAssemblies.Filename)' == 'PresentationCore' Or '%(ReferencePathWithRefAssemblies.Filename)' == 'PresentationFramework' Or '%(ReferencePathWithRefAssemblies.Filename)' == 'ReachFramework' Or '%(ReferencePathWithRefAssemblies.Filename)' == 'System.Printing'" />
              <ReferencePath Include="@(_DotNetCampusWpfReferenceDll)" />
              <ReferencePathWithRefAssemblies Include="@(_DotNetCampusWpfReferenceDll)" />
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

public static void ValidatePackageAssets(string stagingDir)
{
    RequirePackageFile(Path.Join(stagingDir, "buildTransitive", $"{PackageMetadata.Id}.targets"));

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
    var fileInfo = new FileInfo(nupkgPath);
    Log.Info($"  .nupkg generated: {nupkgPath} ({fileInfo.Length / 1024.0:F1} KB)");
    return nupkgPath;
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

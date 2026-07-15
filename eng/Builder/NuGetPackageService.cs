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

        return result;
    }

private static string ReadMsBuildProperty(XDocument document, string propsPath, string propertyName)
{
    var values = document
        .Descendants()
        .Where(element =>
            element.Parent?.Name.LocalName == "PropertyGroup" &&
            element.Name.LocalName == propertyName)
        .Select(element => element.Value)
        .ToList();
    if (values.Count == 0 || string.IsNullOrWhiteSpace(values[0]))
        throw new InvalidOperationException($"MSBuild property '{propertyName}' was not found in {propsPath}");
    if (values.Count > 1)
        throw new InvalidOperationException($"MSBuild property '{propertyName}' is defined multiple times in {propsPath}");

    return values[0];
}

public static IReadOnlyList<PackageDependency> ReadRuntimePackageDependencies(string repoRoot)
{
    var versionsPropsPath = Path.Join(repoRoot, "eng", "Versions.props");
    var document = XDocument.Load(versionsPropsPath);
    return
    [
        new("System.Configuration.ConfigurationManager", ReadMsBuildProperty(document, versionsPropsPath, "SystemConfigurationConfigurationManagerPackageVersion")),
        new("System.Diagnostics.EventLog", ReadMsBuildProperty(document, versionsPropsPath, "SystemDiagnosticsEventLogPackageVersion")),
        new("System.DirectoryServices", ReadMsBuildProperty(document, versionsPropsPath, "SystemDirectoryServicesVersion")),
        new("System.Drawing.Common", ReadMsBuildProperty(document, versionsPropsPath, "SystemDrawingCommonVersion")),
        new("System.Formats.Nrbf", ReadMsBuildProperty(document, versionsPropsPath, "SystemFormatsNrbfVersion")),
        new("System.IO.Packaging", ReadMsBuildProperty(document, versionsPropsPath, "SystemIOPackagingVersion")),
        new("System.Resources.Extensions", ReadMsBuildProperty(document, versionsPropsPath, "SystemResourcesExtensionsVersion")),
        new("System.Security.Cryptography.Xml", ReadMsBuildProperty(document, versionsPropsPath, "SystemSecurityCryptographyXmlPackageVersion")),
        new("System.Security.Permissions", ReadMsBuildProperty(document, versionsPropsPath, "SystemSecurityPermissionsPackageVersion")),
        new("System.Windows.Extensions", ReadMsBuildProperty(document, versionsPropsPath, "SystemWindowsExtensionsPackageVersion")),
    ];
}


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

    var sb = new StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    sb.AppendLine("<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\">");
    sb.AppendLine("  <metadata>");
    sb.AppendLine("    <id>DotNetCampus.WpfLib</id>");
    sb.AppendLine($"    <version>{version}</version>");
    sb.AppendLine("    <authors>dotnet campus</authors>");
    sb.AppendLine("    <description>Custom-built WPF managed assemblies and native runtimes.</description>");
    sb.AppendLine("    <copyright>dotnet campus</copyright>");
    sb.AppendLine("    <license type=\"expression\">MIT</license>");
    sb.AppendLine("    <projectUrl>https://github.com/dotnet-campus/wpf</projectUrl>");
    sb.AppendLine("    <tags>WPF WindowsDesktop</tags>");
    sb.AppendLine("    <dependencies>");
    foreach (var targetFramework in PackageMetadata.TargetFrameworks)
    {
        sb.AppendLine($"      <group targetFramework=\"{targetFramework}\">");
        foreach (var dependency in runtimePackageDependencies)
        {
            sb.AppendLine($"        <dependency id=\"{dependency.Id}\" version=\"[{dependency.Version}, )\" />");
        }
        sb.AppendLine("      </group>");
    }
    sb.AppendLine("    </dependencies>");
    sb.AppendLine("  </metadata>");
    sb.AppendLine("  <files>");

    foreach (var file in referenceFiles)
    {
        sb.AppendLine($"    <file src=\"ref\\net8.0\\{file}\" target=\"ref\\net8.0\\{file}\" />");
    }

    foreach (var rid in new[] { "win-x64", "win-x86" })
    {
        var runtimeLibDir = Path.Join(stagingDir, "runtimes", rid, "lib", "net8.0");
        foreach (var file in Directory.GetFiles(runtimeLibDir, "*.dll").OrderBy(Path.GetFileName))
        {
            var fileName = Path.GetFileName(file);
            sb.AppendLine($"    <file src=\"runtimes\\{rid}\\lib\\net8.0\\{fileName}\" target=\"runtimes\\{rid}\\lib\\net8.0\\{fileName}\" />");
        }

        var nativeDir = Path.Join(stagingDir, "runtimes", rid, "native");
        foreach (var file in Directory.GetFiles(nativeDir, "*.dll").OrderBy(Path.GetFileName))
        {
            var fileName = Path.GetFileName(file);
            sb.AppendLine($"    <file src=\"runtimes\\{rid}\\native\\{fileName}\" target=\"runtimes\\{rid}\\native\\{fileName}\" />");
        }
    }

    sb.AppendLine("    <file src=\"buildTransitive\\DotNetCampus.WpfLib.targets\" target=\"buildTransitive\\DotNetCampus.WpfLib.targets\" />");

    sb.AppendLine("  </files>");
    sb.AppendLine("</package>");

    var nuspecContent = sb.ToString();
    var nuspecPath = Path.Join(stagingDir, "DotNetCampus.WpfLib.nuspec");
    File.WriteAllText(nuspecPath, nuspecContent);
    Log.Info($"  .nuspec generated: {nuspecPath}");
    return nuspecPath;
}

public static void GenerateBuildTransitiveTargets(string stagingDir)
{
    var targetsDir = Path.Join(stagingDir, "buildTransitive");
    Directory.CreateDirectory(targetsDir);
    var targetsPath = Path.Join(targetsDir, "DotNetCampus.WpfLib.targets");
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
    Log.Info("  buildTransitive/DotNetCampus.WpfLib.targets");
}

public static void ValidatePackageAssets(string stagingDir)
{
    RequirePackageFile(Path.Join(stagingDir, "buildTransitive", "DotNetCampus.WpfLib.targets"));

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

private static void RequirePackageFile(string path)
{
    if (!File.Exists(path))
        throw new InvalidOperationException($"Required package asset not found: {path}");
}

public static string PackNuGet(string nuspecPath, string outputDir)
{
    Directory.CreateDirectory(outputDir);

    // Place _pack.csproj outside the repo tree (system temp directory)
    // to avoid inheriting Arcade SDK imports from the repo root Directory.Build.props
    var packDir = Path.Join(Path.GetTempPath(), "WpfBuilderPack", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(packDir);

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
        throw new InvalidOperationException("No generated .nupkg file found");

    var nupkgPath = nupkgFiles.OrderByDescending(File.GetLastWriteTime).First();
    var fileInfo = new FileInfo(nupkgPath);
    Log.Info($"  .nupkg generated: {nupkgPath} ({fileInfo.Length / 1024.0:F1} KB)");
    return nupkgPath;
}
}

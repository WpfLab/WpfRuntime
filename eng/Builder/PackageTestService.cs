using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace WpfReorganize.Builder;

internal static class PackageTestService
{
public static int Run(BuilderContext context, string? packageArg)
{
    var packagePath = ResolvePackagePath(context.NupkgOutputDir, packageArg);
    var packageVersion = ReadPackageVersion(packagePath);
    var testRoot = Path.Join(
        Path.GetDirectoryName(context.NupkgOutputDir)!,
        "package-tests",
        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(testRoot);
    var packageSourceDir = Path.Join(testRoot, "packages");
    Directory.CreateDirectory(packageSourceDir);
    File.Copy(packagePath, Path.Join(packageSourceDir, Path.GetFileName(packagePath)), overwrite: true);
    var extractedPackageDir = Path.Join(testRoot, "extracted-package");
    ZipFile.ExtractToDirectory(packagePath, extractedPackageDir);
    var runtimePackageDependencies = NuGetPackageService.ReadRuntimePackageDependencies(context.RepoRoot);
    ValidatePackageDependencies(packagePath, runtimePackageDependencies, PackageMetadata.TargetFrameworks);

    WritePackageTestGlobalJson(testRoot);
    var nugetConfigPath = WritePackageTestNuGetConfig(testRoot, packageSourceDir);
    var testProjects = CreatePackageTestProjects(context.RepoRoot, testRoot, packageVersion);

    Log.Info($"NuGet package: {packagePath}");
    Log.Info($"Package version: {packageVersion}");
    Log.Info($"Test directory: {testRoot}");
    Log.Info($"NuGet configuration: {nugetConfigPath}");
    foreach (var testProject in testProjects)
    {
        Log.Info($"Test project: {testProject.Name} ({string.Join(", ", testProject.TargetFrameworks)})");
    }

    foreach (var testProject in testProjects)
    {
        foreach (var targetFramework in testProject.TargetFrameworks)
        {
            foreach (var rid in new[] { "win-x86", "win-x64" })
            {
                PublishAndValidatePackageTest(
                    testProject,
                    targetFramework,
                    rid,
                    testRoot,
                    extractedPackageDir,
                    nugetConfigPath,
                    runtimePackageDependencies);
            }
        }
    }

    Log.Info("Package publish validation passed for all projects, target frameworks, and runtime identifiers.");

    return 0;
}

static void PublishAndValidatePackageTest(
    PackageTestProject testProject,
    string targetFramework,
    string rid,
    string testRoot,
    string extractedPackageDir,
    string nugetConfigPath,
    IReadOnlyList<PackageDependency> runtimePackageDependencies)
{
    var publishDir = Path.Join(testRoot, "publish", testProject.Name, targetFramework, rid);
    var restorePackagesDir = Path.Join(testRoot, "restore-packages");
    Directory.CreateDirectory(publishDir);
    Log.Step($"Publishing {testProject.Name} for {targetFramework}/{rid}...");

    var arguments = GetPublishArguments(
        testProject.ProjectPath,
        targetFramework,
        rid,
        nugetConfigPath,
        restorePackagesDir,
        publishDir);
    var result = ProcessRunner.Run("dotnet", arguments, Path.GetDirectoryName(testProject.ProjectPath)!);
    if (result.ExitCode != 0)
    {
        Log.Error(result.Output);
        throw new InvalidOperationException($"Package test publish failed for {testProject.Name} ({targetFramework}/{rid})");
    }

    ValidatePublishedPackageDlls(extractedPackageDir, publishDir, rid, testProject.Name, targetFramework);
    ValidatePublishedFrameworkDependencies(publishDir, testProject.Name, targetFramework, rid);
    ValidatePublishedRuntimeDependencies(publishDir, testProject.Name, targetFramework, rid);
    foreach (var dependency in runtimePackageDependencies)
    {
        ValidatePublishedDependencyDll(publishDir, $"{dependency.Id}.dll", testProject.Name, targetFramework, rid);
    }
    RunPublishedPackageProbe(testProject.Name, targetFramework, rid, publishDir);
}

static void ValidatePackageDependencies(
    string packagePath,
    IReadOnlyList<PackageDependency> expectedDependencies,
    IReadOnlyList<string> targetFrameworks)
{
    using var archive = ZipFile.OpenRead(packagePath);
    var nuspecEntry = archive.Entries.SingleOrDefault(entry =>
        entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Package does not contain a .nuspec file: {packagePath}");

    using var stream = nuspecEntry.Open();
    var document = XDocument.Load(stream);
    var dependencyGroups = document
        .Descendants()
        .Where(element => element.Name.LocalName == "group")
        .ToList();

    foreach (var targetFramework in targetFrameworks)
    {
        var group = dependencyGroups.SingleOrDefault(element =>
            string.Equals((string?)element.Attribute("targetFramework"), targetFramework, StringComparison.OrdinalIgnoreCase));
        if (group is null)
            throw new InvalidOperationException($"Package dependency group is missing for {targetFramework}");

        foreach (var expectedDependency in expectedDependencies)
        {
            var dependency = group
                .Elements()
                .SingleOrDefault(element =>
                    element.Name.LocalName == "dependency" &&
                    string.Equals((string?)element.Attribute("id"), expectedDependency.Id, StringComparison.OrdinalIgnoreCase));
            var actualVersion = (string?)dependency?.Attribute("version");
            var expectedVersionRange = $"[{expectedDependency.Version}, )";
            if (!string.Equals(actualVersion, expectedDependency.Version, StringComparison.Ordinal) &&
                !string.Equals(RemoveWhitespace(actualVersion), RemoveWhitespace(expectedVersionRange), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Package dependency '{expectedDependency.Id}' for {targetFramework} must be {expectedDependency.Version} or {expectedVersionRange}, actual: {actualVersion ?? "missing"}");
            }
        }
    }

    Log.Info($"Validated {expectedDependencies.Count} package dependencies for {string.Join(", ", targetFrameworks)}");
}

static void ValidatePublishedPackageDlls(
    string extractedPackageDir,
    string publishDir,
    string rid,
    string projectName,
    string targetFramework)
{
    var expectedDirectories = new[]
    {
        FindRuntimeLibDirectory(extractedPackageDir, rid),
        Path.Join(extractedPackageDir, "runtimes", rid, "native"),
    };
    foreach (var directory in expectedDirectories)
    {
        if (!Directory.Exists(directory))
            throw new InvalidOperationException($"Expected package asset directory was not found for {rid}: {directory}");
    }

    var runtimeLibDirectory = expectedDirectories[0];
    var nativeDirectory = expectedDirectories[1];
    var expectedFileGroups = expectedDirectories
        .SelectMany(directory => Directory.GetFiles(directory, "*.dll"))
        .GroupBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
        .ToList();
    var unexpectedDuplicateFileNames = expectedFileGroups
        .Where(group => group.Count() > 1 && !string.Equals(group.Key, "ijwhost.dll", StringComparison.OrdinalIgnoreCase))
        .Select(group => group.Key)
        .ToList();
    if (unexpectedDuplicateFileNames.Count > 0)
    {
        throw new InvalidOperationException(
            $"Package contains duplicate DLL file names for {rid}: {string.Join(", ", unexpectedDuplicateFileNames)}");
    }

    var ijwHostFiles = expectedFileGroups
        .SingleOrDefault(group => string.Equals(group.Key, "ijwhost.dll", StringComparison.OrdinalIgnoreCase))?
        .ToList() ?? [];
    var expectedIjwHostFiles = new[]
    {
        Path.Join(runtimeLibDirectory, "ijwhost.dll"),
        Path.Join(nativeDirectory, "ijwhost.dll"),
    };
    if (ijwHostFiles.Count != expectedIjwHostFiles.Length ||
        expectedIjwHostFiles.Any(expectedPath => !ijwHostFiles.Contains(expectedPath, StringComparer.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException(
            $"Package must contain ijwhost.dll in both runtime lib and native directories for {rid}");
    }

    if (!ComputeSha256(expectedIjwHostFiles[0]).SequenceEqual(ComputeSha256(expectedIjwHostFiles[1])))
        throw new InvalidOperationException($"Package contains different ijwhost.dll binaries for {rid}");

    var expectedDlls = expectedFileGroups.ToDictionary(
        group => group.Key,
        group => string.Equals(group.Key, "ijwhost.dll", StringComparison.OrdinalIgnoreCase)
            ? expectedIjwHostFiles[0]
            : group.Single(),
        StringComparer.OrdinalIgnoreCase);

    if (expectedDlls.Count == 0)
        throw new InvalidOperationException($"No expected DLLs were found in the package for {rid}");

    foreach (var (fileName, expectedPath) in expectedDlls)
    {
        var actualPath = Path.Join(publishDir, fileName);
        if (!File.Exists(actualPath))
            throw new InvalidOperationException($"Published package DLL is missing for {projectName} ({targetFramework}/{rid}): {actualPath}");

        var expectedHash = ComputeSha256(expectedPath);
        var actualHash = ComputeSha256(actualPath);
        if (!expectedHash.SequenceEqual(actualHash))
        {
            throw new InvalidOperationException(
                $"Published package DLL does not match the {rid} package asset for {projectName} ({targetFramework}): {fileName}. " +
                $"Expected {Convert.ToHexString(expectedHash)} from {expectedPath}; actual {Convert.ToHexString(actualHash)} from {actualPath}");
        }
    }

    Log.Info($"Validated {expectedDlls.Count} package DLLs for {projectName} ({targetFramework}/{rid})");
}

internal static void ValidatePublishedFrameworkDependencies(
    string publishDir,
    string projectName,
    string targetFramework,
    string rid)
{
    var runtimeConfigPath = Path.Join(publishDir, $"{projectName}.runtimeconfig.json");
    if (!File.Exists(runtimeConfigPath))
        throw new InvalidOperationException($"Published runtime configuration is missing: {runtimeConfigPath}");

    using var document = JsonDocument.Parse(File.ReadAllBytes(runtimeConfigPath));
    var runtimeOptions = document.RootElement.GetProperty("runtimeOptions");
    var frameworkNames = new List<string>();
    if (runtimeOptions.TryGetProperty("framework", out var framework))
        frameworkNames.Add(framework.GetProperty("name").GetString() ?? string.Empty);

    if (runtimeOptions.TryGetProperty("frameworks", out var frameworks))
    {
        frameworkNames.AddRange(frameworks.EnumerateArray().Select(item =>
            item.GetProperty("name").GetString() ?? string.Empty));
    }

    if (!frameworkNames.Contains("Microsoft.NETCore.App", StringComparer.Ordinal))
    {
        throw new InvalidOperationException(
            $"Published application must retain Microsoft.NETCore.App for {projectName} ({targetFramework}/{rid}).");
    }

    var windowsDesktopFramework = frameworkNames.FirstOrDefault(name =>
        name.StartsWith("Microsoft.WindowsDesktop.App", StringComparison.Ordinal));
    if (windowsDesktopFramework is not null)
    {
        throw new InvalidOperationException(
            $"Published application must not depend on {windowsDesktopFramework} for {projectName} ({targetFramework}/{rid}); " +
            "the shared framework would place its DirectWriteForwarder assembly in the trusted platform assembly set.");
    }

    Log.Info($"Validated framework dependencies for {projectName} ({targetFramework}/{rid}): {string.Join(", ", frameworkNames)}");
}

internal static void ValidatePublishedRuntimeDependencies(
    string publishDir,
    string projectName,
    string targetFramework,
    string rid)
{
    var depsPath = Path.Join(publishDir, $"{projectName}.deps.json");
    if (!File.Exists(depsPath))
        throw new InvalidOperationException($"Published dependency manifest is missing: {depsPath}");

    using var document = JsonDocument.Parse(File.ReadAllBytes(depsPath));
    var containsDirectWriteForwarder = document.RootElement
        .GetProperty("targets")
        .EnumerateObject()
        .SelectMany(target => target.Value.EnumerateObject())
        .Any(library =>
            library.Value.TryGetProperty("runtime", out var runtimeAssets) &&
            runtimeAssets.EnumerateObject().Any(asset =>
                string.Equals(Path.GetFileName(asset.Name), "DirectWriteForwarder.dll", StringComparison.OrdinalIgnoreCase)));

    if (!containsDirectWriteForwarder)
    {
        throw new InvalidOperationException(
            $"Published dependency manifest must contain DirectWriteForwarder.dll for {projectName} ({targetFramework}/{rid}); " +
            "copying the file without registering it as a runtime asset does not override host assembly resolution.");
    }

    Log.Info($"Validated DirectWriteForwarder runtime dependency for {projectName} ({targetFramework}/{rid})");
}

static void ValidatePublishedDependencyDll(
    string publishDir,
    string fileName,
    string projectName,
    string targetFramework,
    string rid)
{
    var dependencyPath = Path.Join(publishDir, fileName);
    if (!File.Exists(dependencyPath))
    {
        throw new InvalidOperationException(
            $"Published NuGet dependency is missing for {projectName} ({targetFramework}/{rid}): {dependencyPath}");
    }

    Log.Info($"Validated published dependency {fileName} for {projectName} ({targetFramework}/{rid})");
}

static string FindRuntimeLibDirectory(string extractedPackageDir, string rid)
{
    var libRoot = Path.Join(extractedPackageDir, "runtimes", rid, "lib");
    if (!Directory.Exists(libRoot))
        throw new InvalidOperationException($"Package runtime lib directory was not found for {rid}: {libRoot}");

    var frameworkDirectories = Directory.GetDirectories(libRoot);
    if (frameworkDirectories.Length != 1)
    {
        throw new InvalidOperationException(
            $"Expected exactly one runtime asset framework directory for {rid}, found {frameworkDirectories.Length}: {libRoot}");
    }

    return frameworkDirectories[0];
}

static byte[] ComputeSha256(string path)
{
    using var stream = File.OpenRead(path);
    return SHA256.HashData(stream);
}

static void RunPublishedPackageProbe(string projectName, string targetFramework, string rid, string publishDir)
{
    var executablePath = Path.Join(publishDir, $"{projectName}.exe");
    if (!File.Exists(executablePath))
        throw new InvalidOperationException($"Published package test executable was not found: {executablePath}");

    Log.Info($"Running {projectName} ({targetFramework}/{rid})...");
    var result = ProcessRunner.Run(executablePath, "", publishDir, TimeSpan.FromSeconds(30));
    if (result.ExitCode != 0)
    {
        Log.Error(result.Output);
        throw new InvalidOperationException(
            $"Published package test failed for {projectName} ({targetFramework}/{rid}) with exit code {result.ExitCode}");
    }

    if (!string.IsNullOrWhiteSpace(result.Output))
        Log.Info(result.Output.Trim());

    Log.Info($"Probe completed for {projectName} ({targetFramework}/{rid}) in {result.Elapsed.TotalSeconds:F1}s");
}

static string ReadPackageVersion(string packagePath)
{
    using var archive = ZipFile.OpenRead(packagePath);
    var nuspecEntry = archive.Entries.SingleOrDefault(entry =>
        entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Package does not contain a .nuspec file: {packagePath}");

    using var stream = nuspecEntry.Open();
    var document = XDocument.Load(stream);
    var metadata = document.Root?.Elements().SingleOrDefault(element => element.Name.LocalName == "metadata")
        ?? throw new InvalidOperationException($"Package metadata was not found in {packagePath}");
    var version = metadata.Elements().SingleOrDefault(element => element.Name.LocalName == "version")?.Value;
    if (string.IsNullOrWhiteSpace(version))
        throw new InvalidOperationException($"Package version was not found in {packagePath}");

    return version;
}

static void WritePackageTestGlobalJson(string testRoot)
{
    var content = """
        {
          "sdk": {
            "version": "9.0.100",
            "rollForward": "latestMajor",
            "allowPrerelease": false
          }
        }
        """;
    File.WriteAllText(Path.Join(testRoot, "global.json"), content);
}

static string WritePackageTestNuGetConfig(string testRoot, string packageSourceDir)
{
    var path = Path.Join(testRoot, "NuGet.Config");
    var content = $"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="package-under-test" value="{XmlEscape(packageSourceDir)}" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
          </packageSources>
        </configuration>
        """;
    File.WriteAllText(path, content);
    return path;
}

static IReadOnlyList<PackageTestProject> CreatePackageTestProjects(string repoRoot, string testRoot, string packageVersion)
{
    return
    [
        CreatePackageTestProject(repoRoot, testRoot, "SingleNet8", "TargetFramework", "net8.0-windows", ["net8.0-windows"], packageVersion),
        CreatePackageTestProject(repoRoot, testRoot, "SingleNet9", "TargetFramework", "net9.0-windows", ["net9.0-windows"], packageVersion),
        CreatePackageTestProject(repoRoot, testRoot, "MultiTarget", "TargetFrameworks", "net8.0-windows;net9.0-windows", ["net8.0-windows", "net9.0-windows"], packageVersion),
    ];
}

static PackageTestProject CreatePackageTestProject(
    string repoRoot,
    string testRoot,
    string name,
    string targetFrameworkPropertyName,
    string targetFrameworkPropertyValue,
    IReadOnlyList<string> targetFrameworks,
    string packageVersion)
{
    var templateDir = Path.Join(repoRoot, "eng", "Builder", "PackageTestApp");
    if (!Directory.Exists(templateDir))
        throw new DirectoryNotFoundException($"Package test application template was not found: {templateDir}");

    var projectDir = Path.Join(testRoot, name);
    CopyPackageTestProjectTemplate(templateDir, projectDir);

    var projectPath = Path.Join(projectDir, "PackageTestApp.csproj");
    var document = XDocument.Load(projectPath);
    var propertyGroup = document.Root?.Elements("PropertyGroup").FirstOrDefault()
        ?? throw new InvalidOperationException($"Package test project has no PropertyGroup: {projectPath}");
    propertyGroup.Element("TargetFramework")?.Remove();
    propertyGroup.Element("TargetFrameworks")?.Remove();
    propertyGroup.Add(new XElement(targetFrameworkPropertyName, targetFrameworkPropertyValue));
    propertyGroup.SetElementValue("AssemblyName", name);

    var packageReference = document
        .Descendants("PackageReference")
        .SingleOrDefault(element => string.Equals((string?)element.Attribute("Include"), PackageMetadata.Id, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"Package test project does not reference {PackageMetadata.Id}: {projectPath}");
    packageReference.SetAttributeValue("Version", packageVersion);
    document.Save(projectPath);

    return new PackageTestProject(name, projectPath, targetFrameworks);
}

static void CopyPackageTestProjectTemplate(string sourceDir, string destinationDir)
{
    Directory.CreateDirectory(destinationDir);
    foreach (var sourcePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
    {
        var relativePath = Path.GetRelativePath(sourceDir, sourcePath);
        if (relativePath.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            continue;

        var destinationPath = Path.Join(destinationDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }
}

internal static string GetPublishArguments(
    string projectPath,
    string targetFramework,
    string rid,
    string nugetConfigPath,
    string restorePackagesDir,
    string publishDir) =>
    $"publish \"{projectPath}\" --configuration Release --framework {targetFramework} --runtime {rid} --self-contained false --configfile \"{nugetConfigPath}\" --packages \"{restorePackagesDir}\" --output \"{publishDir}\" --nologo --property:WpfRuntimeReferenceDiagnostics=true --property:GenerateTemporaryTargetAssemblyDebuggingInformation=true";

static string XmlEscape(string value) =>
    value.Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

static string RemoveWhitespace(string? value) =>
    value is null ? string.Empty : string.Concat(value.Where(character => !char.IsWhiteSpace(character)));

static string ResolvePackagePath(string nupkgOutputDir, string? packageArg)
{
    if (!string.IsNullOrWhiteSpace(packageArg))
    {
        var fullPath = Path.GetFullPath(packageArg);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("NuGet package not found", fullPath);

        return fullPath;
    }

    if (!Directory.Exists(nupkgOutputDir))
        throw new DirectoryNotFoundException($"NuGet package output directory not found: {nupkgOutputDir}");

    return Directory.GetFiles(nupkgOutputDir, $"{PackageMetadata.Id}.*.nupkg")
        .OrderByDescending(File.GetLastWriteTimeUtc)
        .FirstOrDefault()
        ?? throw new InvalidOperationException($"No {PackageMetadata.Id} package found in {nupkgOutputDir}");
}
}

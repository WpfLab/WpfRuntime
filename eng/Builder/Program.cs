using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// ===========================================================
// DotNetCampus.WpfLib Builder — Build driver + NuGet packaging tool
//
// Usage:
//   dotnet run --project eng\Builder\Builder.csproj -- clean
//       Clean all build outputs (artifacts/, bin/, obj/, .vs/) to simulate
//       a fresh clone.  Skips files locked by Visual Studio gracefully.
//
//   dotnet run --project eng\Builder\Builder.csproj [--version <ver>]
//       Full build + NuGet packaging pipeline.
//       --version <ver>  Override package version (default: 1.0.0).
//                        In CI, pass $(Build.BuildNumber) or git SHA.
//
//   dotnet run --project eng\Builder\Builder.csproj -- compare
//       Compare built DLLs against official Microsoft.WindowsDesktop.App.Ref
//       to detect missing/extra assemblies.  Run after a build.
//
//   dotnet run --project eng\Builder\Builder.csproj -- test-package [--package <path>]
//       Publish and run temporary WPF projects against the generated NuGet
//       package.  When --package is omitted, uses the newest local .nupkg.
// ===========================================================

var repoRoot = FindRepoRoot();
var artifactsDir = Path.Join(repoRoot, "artifacts");
var buildLogsDir = Path.Join(artifactsDir, "log", "Builder");
var builderOutputDir = Path.Join(repoRoot, "eng", "Builder", "bin");
var stagingDir = Path.Join(builderOutputDir, "staging");
var nupkgOutputDir = Path.Join(builderOutputDir, "nupkg");

// ---- Parse command-line arguments ----
var cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToList();
var command = cmdArgs.FirstOrDefault(a => !a.StartsWith("--"))?.ToLowerInvariant();
var versionArg = cmdArgs.SkipWhile(a => a != "--version").Skip(1).FirstOrDefault();
var version = string.IsNullOrEmpty(versionArg) ? "1.0.0" : versionArg;
var packageArg = cmdArgs.SkipWhile(a => a != "--package").Skip(1).FirstOrDefault();

if (command == "clean")
{
    Log.Info("=== DotNetCampus.WpfLib Builder — Clean Mode ===");
    Log.Info($"Repo root: {repoRoot}");
    RunClean(repoRoot, artifactsDir, builderOutputDir);
    return 0;
}

if (command == "compare")
{
    Log.Info("=== DotNetCampus.WpfLib Builder — Compare Mode ===");
    Log.Info($"Repo root: {repoRoot}");
    return RunCompare(builderOutputDir, stagingDir);
}

if (command == "test-package")
{
    Log.Info("=== DotNetCampus.WpfLib Builder — Package Test Mode ===");
    Log.Info($"Repo root: {repoRoot}");
    return RunPackageTests(nupkgOutputDir, packageArg);
}

var startTime = Stopwatch.GetTimestamp();

Log.Info("=== DotNetCampus.WpfLib Builder ===");
Log.Info($"Repo root: {repoRoot}");

// ---- Step 1: Clean artifacts ----
Log.Step("Cleaning artifacts folder...");
CleanArtifacts(artifactsDir);
Directory.CreateDirectory(buildLogsDir);

// ---- Step 2: Clean staging directory ----
Log.Step("Cleaning staging directory...");
if (Directory.Exists(stagingDir))
    Directory.Delete(stagingDir, recursive: true);

// ---- Step 3: Build projects ----
Log.Step("Building projects (x64 + x86)...");
var srcDir = Path.Join(repoRoot, "src", "Microsoft.DotNet.Wpf", "src");
var msbuildExe = FindMsBuild();
Log.Info($"  MSBuild: {msbuildExe}");

// Build in dependency order: build dependencies first, then their dependents
var projectsToBuild = new[]
{
    Path.Join(srcDir, "WindowsBase", "WindowsBase.csproj"),
    Path.Join(srcDir, "System.Xaml", "System.Xaml.csproj"),
    Path.Join(srcDir, "UIAutomation", "UIAutomationTypes", "UIAutomationTypes.csproj"),
    Path.Join(srcDir, "UIAutomation", "UIAutomationProvider", "UIAutomationProvider.csproj"),
    Path.Join(srcDir, "DirectWriteForwarder", "DirectWriteForwarder.vcxproj"),
    Path.Join(srcDir, "PresentationCore", "PresentationCore.csproj"),
    Path.Join(srcDir, "UIAutomation", "UIAutomationClient", "UIAutomationClient.csproj"),
    Path.Join(srcDir, "UIAutomation", "UIAutomationClientSideProviders", "UIAutomationClientSideProviders.csproj"),
    Path.Join(srcDir, "PresentationFramework", "PresentationFramework.csproj"),
    Path.Join(srcDir, "ReachFramework", "ReachFramework.csproj"),
    Path.Join(srcDir, "System.Windows.Presentation", "System.Windows.Presentation.csproj"),
    Path.Join(srcDir, "System.Windows.Input.Manipulations", "System.Windows.Input.Manipulations.csproj"),
    Path.Join(srcDir, "PresentationUI", "PresentationUI.csproj"),
    Path.Join(srcDir, "System.Windows.Controls.Ribbon", "System.Windows.Controls.Ribbon.csproj"),
    Path.Join(srcDir, "WindowsFormsIntegration", "WindowsFormsIntegration.csproj"),
    Path.Join(srcDir, "Themes", "PresentationFramework.Aero", "PresentationFramework.Aero.csproj"),
    Path.Join(srcDir, "Themes", "PresentationFramework.Aero2", "PresentationFramework.Aero2.csproj"),
    Path.Join(srcDir, "Themes", "PresentationFramework.AeroLite", "PresentationFramework.AeroLite.csproj"),
    Path.Join(srcDir, "Themes", "PresentationFramework.Classic", "PresentationFramework.Classic.csproj"),
    Path.Join(srcDir, "Themes", "PresentationFramework.Fluent", "PresentationFramework.Fluent.csproj"),
    Path.Join(srcDir, "Themes", "PresentationFramework.Luna", "PresentationFramework.Luna.csproj"),
    Path.Join(srcDir, "Themes", "PresentationFramework.Royale", "PresentationFramework.Royale.csproj"),
};

var failedProjects = new List<string>();

// PresentationBuildTasks is an MSBuild task assembly rather than a runtime asset.
// Its lookup path includes WpfNativePlatform, so prebuild one copy per runtime architecture.
var presentationBuildTasksPath = Path.Join(srcDir, "PresentationBuildTasks", "PresentationBuildTasks.csproj");
if (!File.Exists(presentationBuildTasksPath))
{
    Log.Error($"PresentationBuildTasks project not found: {presentationBuildTasksPath}");
    return 1;
}

foreach (var platform in new[] { "x64", "x86" })
{
    var projectName = "PresentationBuildTasks";
    var logPath = GetBuildLogPath(buildLogsDir, projectName, platform);
    var arguments = $"\"{presentationBuildTasksPath}\" -restore /p:Configuration=Debug /p:Platform={platform} /p:TargetFramework=net472 /m:1 /nr:false /v:minimal /clp:ErrorsOnly{GetFileLoggerArguments(logPath)}";
    var presentationBuildTasksResult = RunProcess(
        msbuildExe,
        arguments,
        repoRoot);
    if (presentationBuildTasksResult.ExitCode != 0)
    {
        LogBuildFailure(projectName, platform, msbuildExe, arguments, repoRoot, logPath, presentationBuildTasksResult);
        failedProjects.Add($"{projectName} ({platform})");
    }
}

foreach (var platform in new[] { "x64", "x86" })
{
    Log.Info($"  --- Building {platform} runtime assemblies ---");

    foreach (var projectPath in projectsToBuild)
    {
        if (!File.Exists(projectPath))
        {
            Log.Warn($"Project not found, skipping: {projectPath}");
            continue;
        }

        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var projectPlatform = platform == "x86" && Path.GetExtension(projectPath).Equals(".vcxproj", StringComparison.OrdinalIgnoreCase)
            ? "Win32"
            : platform;
        Log.Info($"  Building {projectName} ({platform})...");

        var logPath = GetBuildLogPath(buildLogsDir, projectName, platform);
        var arguments = $"\"{projectPath}\" -restore /p:Configuration=Debug /p:Platform={projectPlatform} /p:UsePrebuiltPresentationBuildTasks=true /p:BuildPresentationBuildTasksOnDemand=false /m:1 /nr:false /v:minimal /clp:ErrorsOnly{GetFileLoggerArguments(logPath)}";
        var result = RunProcess(
            msbuildExe,
            arguments,
            repoRoot);
        if (result.ExitCode != 0)
        {
            LogBuildFailure(projectName, platform, msbuildExe, arguments, repoRoot, logPath, result);
            failedProjects.Add($"{projectName} ({platform})");
            // Continue building remaining projects; do not abort immediately
        }
    }
}

if (failedProjects.Count > 0)
{
    Log.Warn($"The following projects failed to build (their DLLs will be skipped): {string.Join(", ", failedProjects)}");
    Log.Warn($"Diagnostic MSBuild logs: {buildLogsDir}");
}
else
{
    Log.Info("All projects built successfully");
}

// Whether to abort on build failures.  In CI we want to continue packaging
// so that partial results can be inspected, but the final exit code reflects
// the failure (handled at the end).

// ---- Step 4: Collect reference and runtime DLLs ----
Log.Step("Collecting reference assemblies...");
var referenceDlls = CollectReferenceDlls(artifactsDir);
if (referenceDlls.Count == 0)
{
    Log.Error("No reference assemblies found; please check build artifacts");
    return 1;
}

var refDir = Path.Join(stagingDir, "ref", "net8.0");
Directory.CreateDirectory(refDir);
foreach (var (name, sourcePath) in referenceDlls)
{
    var destPath = Path.Join(refDir, name);
    File.Copy(sourcePath, destPath, overwrite: true);
    Log.Info($"  ref/net8.0/{name}");
}

Log.Step("Collecting architecture-specific runtime assemblies...");
foreach (var (rid, platform) in new[] { ("win-x64", "x64"), ("win-x86", "x86") })
{
    var runtimeDlls = CollectRuntimeDlls(artifactsDir, platform);
    if (runtimeDlls.Count == 0)
    {
        Log.Error($"No runtime assemblies found for {rid}; please check build artifacts");
        return 1;
    }

    var runtimeLibDir = Path.Join(stagingDir, "runtimes", rid, "lib", "net8.0");
    Directory.CreateDirectory(runtimeLibDir);
    foreach (var (name, sourcePath) in runtimeDlls)
    {
        var destPath = Path.Join(runtimeLibDir, name);
        File.Copy(sourcePath, destPath, overwrite: true);
        Log.Info($"  runtimes/{rid}/lib/net8.0/{name}");
    }
}

// ---- Step 5: Collect native DLLs ----
Log.Step("Collecting native DLLs...");
var packagePaths = ReadPackagePaths(builderOutputDir);
var runtimesDir = Path.Join(stagingDir, "runtimes");
CopyNativeDllsFromPackage(packagePaths, "win-x64", Path.Join(runtimesDir, "win-x64", "native"));
CopyNativeDllsFromPackage(packagePaths, "win-x86", Path.Join(runtimesDir, "win-x86", "native"));
CopyIjwHostFromPackage(packagePaths, "win-x64", Path.Join(runtimesDir, "win-x64", "native"));
CopyIjwHostFromPackage(packagePaths, "win-x86", Path.Join(runtimesDir, "win-x86", "native"));

// ---- Step 6: Generate .nuspec and pack ----
Log.Step("Generating .nuspec and packing...");
Log.Info($"  Package version: {version}");
GenerateBuildTransitiveTargets(stagingDir);
try
{
    ValidatePackageAssets(stagingDir);
}
catch (InvalidOperationException exception)
{
    Log.Error(exception.Message);
    return 1;
}
var runtimePackageDependencies = ReadRuntimePackageDependencies(repoRoot);
var nuspecPath = GenerateNuspec(stagingDir, version, runtimePackageDependencies);
var nupkgPath = PackNuGet(nuspecPath, nupkgOutputDir);

// ---- Step 7: Compare against official package ----
Log.Step("Comparing against official Microsoft.WindowsDesktop.App.Ref...");
RunCompare(builderOutputDir, stagingDir, reportOnly: true);

// ---- Done ----
var elapsed = Stopwatch.GetElapsedTime(startTime);
Log.Info("========================================");
Log.Info($"Build complete! Elapsed: {elapsed.TotalSeconds:F1}s");
Log.Info($"NuGet package: {nupkgPath}");
// Non-zero exit if any projects failed — CI will flag this
return failedProjects.Count > 0 ? 2 : 0;

// ===========================================================
// Helper methods
// ===========================================================

static int RunPackageTests(string nupkgOutputDir, string? packageArg)
{
    var packagePath = ResolvePackagePath(nupkgOutputDir, packageArg);
    var packageVersion = ReadPackageVersion(packagePath);
    var testRoot = Path.Join(
        Path.GetDirectoryName(nupkgOutputDir)!,
        "package-tests",
        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(testRoot);
    var packageSourceDir = Path.Join(testRoot, "packages");
    Directory.CreateDirectory(packageSourceDir);
    File.Copy(packagePath, Path.Join(packageSourceDir, Path.GetFileName(packagePath)), overwrite: true);
    var extractedPackageDir = Path.Join(testRoot, "extracted-package");
    ZipFile.ExtractToDirectory(packagePath, extractedPackageDir);
    var runtimePackageDependencies = ReadRuntimePackageDependencies(FindRepoRoot());
    ValidatePackageDependencies(packagePath, runtimePackageDependencies, PackageMetadata.TargetFrameworks);

    WritePackageTestGlobalJson(testRoot);
    var nugetConfigPath = WritePackageTestNuGetConfig(testRoot, packageSourceDir);
    var testProjects = CreatePackageTestProjects(testRoot, packageVersion);

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

    var arguments = $"publish \"{testProject.ProjectPath}\" --configuration Release --framework {targetFramework} --runtime {rid} --self-contained true --configfile \"{nugetConfigPath}\" --packages \"{restorePackagesDir}\" --output \"{publishDir}\" --nologo";
    var result = RunProcess("dotnet", arguments, Path.GetDirectoryName(testProject.ProjectPath)!);
    if (result.ExitCode != 0)
    {
        Log.Error(result.Output);
        throw new InvalidOperationException($"Package test publish failed for {testProject.Name} ({targetFramework}/{rid})");
    }

    ValidatePublishedPackageDlls(extractedPackageDir, publishDir, rid, testProject.Name, targetFramework);
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

    var expectedFiles = expectedDirectories.SelectMany(directory => Directory.GetFiles(directory, "*.dll")).ToList();
    var duplicateFileNames = expectedFiles
        .GroupBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToList();
    if (duplicateFileNames.Count > 0)
    {
        throw new InvalidOperationException(
            $"Package contains duplicate DLL file names for {rid}: {string.Join(", ", duplicateFileNames)}");
    }

    var expectedDlls = expectedFiles
        .ToDictionary(path => Path.GetFileName(path)!, StringComparer.OrdinalIgnoreCase);

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
    var result = RunProcess(executablePath, "", publishDir, TimeSpan.FromSeconds(30));
    if (result.ExitCode != 0)
    {
        Log.Error(result.Output);
        throw new InvalidOperationException(
            $"Published package test failed for {projectName} ({targetFramework}/{rid}) with exit code {result.ExitCode}");
    }

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

static IReadOnlyList<PackageTestProject> CreatePackageTestProjects(string testRoot, string packageVersion)
{
    return
    [
        CreatePackageTestProject(testRoot, "SingleNet8", "<TargetFramework>net8.0-windows</TargetFramework>", ["net8.0-windows"], packageVersion),
        CreatePackageTestProject(testRoot, "SingleNet9", "<TargetFramework>net9.0-windows</TargetFramework>", ["net9.0-windows"], packageVersion),
        CreatePackageTestProject(testRoot, "MultiTarget", "<TargetFrameworks>net8.0-windows;net9.0-windows</TargetFrameworks>", ["net8.0-windows", "net9.0-windows"], packageVersion),
    ];
}

static PackageTestProject CreatePackageTestProject(
    string testRoot,
    string name,
    string targetFrameworkProperty,
    IReadOnlyList<string> targetFrameworks,
    string packageVersion)
{
    var projectDir = Path.Join(testRoot, name);
    Directory.CreateDirectory(projectDir);
    var projectPath = Path.Join(projectDir, $"{name}.csproj");
    var projectContent = $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            {targetFrameworkProperty}
            <UseWPF>true</UseWPF>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <AppendRuntimeIdentifierToOutputPath>true</AppendRuntimeIdentifierToOutputPath>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="DotNetCampus.WpfLib" Version="{XmlEscape(packageVersion)}" />
          </ItemGroup>
        </Project>
        """;
    File.WriteAllText(projectPath, projectContent);

    var programContent = """
        using System.Windows;
        using System.Windows.Threading;

        internal static class Program
        {
            [STAThread]
            private static int Main()
            {
                var application = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown,
                };
                application.Dispatcher.BeginInvoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(application.Shutdown));
                application.Run();
                Console.WriteLine($"WPF package probe completed on {Environment.ProcessPath}.");
                return 0;
            }
        }
        """;
    File.WriteAllText(Path.Join(projectDir, "Program.cs"), programContent);

    return new PackageTestProject(name, projectPath, targetFrameworks);
}

static string XmlEscape(string value) =>
    value.Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);

static string RemoveWhitespace(string? value) =>
    value is null ? string.Empty : string.Concat(value.Where(character => !char.IsWhiteSpace(character)));

static string ReadMsBuildProperty(XDocument document, string propsPath, string propertyName)
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

static IReadOnlyList<PackageDependency> ReadRuntimePackageDependencies(string repoRoot)
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

    return Directory.GetFiles(nupkgOutputDir, "DotNetCampus.WpfLib.*.nupkg")
        .OrderByDescending(File.GetLastWriteTimeUtc)
        .FirstOrDefault()
        ?? throw new InvalidOperationException($"No DotNetCampus.WpfLib package found in {nupkgOutputDir}");
}

static string FindRepoRoot()
{
    var dir = AppContext.BaseDirectory;
    while (dir is not null)
    {
        if (Directory.Exists(Path.Join(dir, ".git")))
            return dir;
        var parent = Path.GetDirectoryName(dir);
        if (parent == dir) break;
        dir = parent;
    }
    throw new InvalidOperationException("Unable to find repository root (.git directory)");
}

/// <summary>
/// Locate MSBuild.exe.  Tries (1) vswhere, (2) PATH, (3) well-known VS paths.
/// Works on both local dev machines and GitHub Actions windows-latest runners.
/// </summary>
static string FindMsBuild()
{
    // 1. Use vswhere to find the latest Visual Studio installation
    var vswhere = Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        "Microsoft Visual Studio", "Installer", "vswhere.exe");

    if (File.Exists(vswhere))
    {
        foreach (var pattern in new[]
        {
            "MSBuild\\**\\Bin\\amd64\\MSBuild.exe",
            "MSBuild\\**\\Bin\\MSBuild.exe",
        })
        {
            var result = RunProcess(vswhere,
                $"-latest -requires Microsoft.Component.MSBuild -find {pattern}",
                AppContext.BaseDirectory);
            if (result.ExitCode == 0)
            {
                var path = result.Output.Trim();
                // Take the first line (vswhere may return multiple paths)
                path = path.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    Log.Info($"  MSBuild found via vswhere: {path}");
                    return path;
                }
            }
        }
    }

    // 2. Try "msbuild" on PATH (common on dev machines with VS Developer prompt)
    var pathResult = RunProcess("where", "msbuild", AppContext.BaseDirectory);
    if (pathResult.ExitCode == 0)
    {
        var path = pathResult.Output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            return path;
    }

    // 3. Fall back to well-known VS paths
    var candidates = new List<string>();
    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    foreach (var edition in new[] { "Enterprise", "Professional", "Community", "BuildTools" })
    {
        candidates.Add(Path.Join(programFiles, "Microsoft Visual Studio", "2022", edition, "MSBuild", "Current", "Bin", "MSBuild.exe"));
        candidates.Add(Path.Join(programFiles, "Microsoft Visual Studio", "2026", edition, "MSBuild", "Current", "Bin", "MSBuild.exe"));
    }

    foreach (var candidate in candidates)
    {
        if (File.Exists(candidate))
            return candidate;
    }

    // 4. Last resort: return "msbuild" and hope it's on PATH
    Log.Warn("Could not locate MSBuild.exe via vswhere or well-known paths; falling back to 'msbuild' on PATH");
    return "msbuild";
}

static string GetBuildLogPath(string buildLogsDir, string projectName, string platform)
{
    var invalidFileNameChars = Path.GetInvalidFileNameChars();
    var safeProjectName = new string(projectName.Select(character => invalidFileNameChars.Contains(character) ? '_' : character).ToArray());
    return Path.Join(buildLogsDir, $"{safeProjectName}-{platform}.log");
}

static string GetFileLoggerArguments(string logPath) =>
    $" /fl /flp:\"logfile={logPath};verbosity=diagnostic;encoding=UTF-8\"";

static void LogBuildFailure(
    string projectName,
    string platform,
    string msbuildExe,
    string arguments,
    string workingDirectory,
    string logPath,
    ProcessResult result)
{
    Log.Error($"Build failed: {projectName} ({platform})");
    Log.Error($"Exit code: {result.ExitCode}; elapsed: {result.Elapsed.TotalSeconds:F1}s");
    Log.Error($"Working directory: {workingDirectory}");
    Log.Error($"Command: \"{msbuildExe}\" {arguments}");

    if (!string.IsNullOrWhiteSpace(result.Output))
    {
        Log.Error("MSBuild console output:");
        WriteIndentedErrorLines(result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
    }
    else
    {
        Log.Error("MSBuild produced no console error output. Reading the diagnostic file log instead.");
    }

    if (!File.Exists(logPath))
    {
        Log.Error($"Diagnostic MSBuild log was not created: {logPath}");
        return;
    }

    Log.Error($"Diagnostic MSBuild log: {logPath}");
    var errorLines = new Queue<string>();
    var logTail = new Queue<string>();
    var uniqueErrorLines = new HashSet<string>(StringComparer.Ordinal);
    foreach (var line in File.ReadLines(logPath))
    {
        EnqueueWithLimit(logTail, line, 100);
        if (IsDiagnosticErrorLine(line) && uniqueErrorLines.Add(line))
        {
            EnqueueWithLimit(errorLines, line, 100);
        }
    }

    if (errorLines.Count > 0)
    {
        Log.Error("Error lines from diagnostic log:");
        WriteIndentedErrorLines(errorLines);
    }
    else
    {
        Log.Error("No explicit error line was found in the diagnostic log.");
    }

    Log.Error("Last 100 lines from diagnostic log:");
    WriteIndentedErrorLines(logTail);
}

static bool IsDiagnosticErrorLine(string line) =>
    line.Contains(" error ", StringComparison.OrdinalIgnoreCase)
    || line.Contains(": error", StringComparison.OrdinalIgnoreCase)
    || line.Contains("exception", StringComparison.OrdinalIgnoreCase)
    || (line.Contains("MSB", StringComparison.OrdinalIgnoreCase)
        && line.Contains("error", StringComparison.OrdinalIgnoreCase));

static void EnqueueWithLimit(Queue<string> lines, string line, int limit)
{
    lines.Enqueue(line);
    if (lines.Count > limit)
    {
        lines.Dequeue();
    }
}

static void WriteIndentedErrorLines(IEnumerable<string> lines)
{
    foreach (var line in lines)
    {
        Console.Error.WriteLine($"      {line}");
    }
}

// ===========================================================
// Compare mode — diff built DLLs against official
// Microsoft.WindowsDesktop.App.Ref package
// ===========================================================

/// <summary>
/// Compare the built managed DLLs against the official WindowsDesktop.App.Ref
/// reference assemblies.  Reports missing, extra, and size-mismatched DLLs.
/// Returns 0 if no issues, 1 if issues found.
/// </summary>
static int RunCompare(string builderOutputDir, string stagingDir, bool reportOnly = false)
{
    var packagePaths = ReadPackagePaths(builderOutputDir);
    if (!packagePaths.TryGetValue("ref", out var refPackageRoot))
    {
        Log.Error("Reference package path not found in PackagePaths.txt; cannot compare");
        return 1;
    }

    // Official ref assemblies: <pkg>/ref/net8.0/
    var refDir = Path.Join(refPackageRoot, "ref", "net8.0");
    if (!Directory.Exists(refDir))
    {
        Log.Error($"Official ref directory not found: {refDir}");
        return 1;
    }

    // Our reference assemblies: staging/ref/net8.0/ (after build) or artifacts/bin (fallback)
    var ourDir = Path.Join(stagingDir, "ref", "net8.0");
    if (!Directory.Exists(ourDir))
    {
        Log.Warn($"Staging dir not found: {ourDir}; looking in artifacts/bin...");
        var repoRoot = FindRepoRoot();
        var artifactsBin = Path.Join(repoRoot, "artifacts", "bin");
        if (Directory.Exists(artifactsBin))
        {
            // Collect from artifacts into a temporary dictionary for comparison
            var collected = CollectReferenceDlls(Path.Join(repoRoot, "artifacts"));
            if (collected.Count == 0)
            {
                Log.Error("No built DLLs found; run a build first");
                return 1;
            }
            ourDir = Path.GetDirectoryName(collected.First().Value)!;
        }
        else
        {
            Log.Error("No built DLLs found; run a build first");
            return 1;
        }
    }

    // WPF assemblies we care about
    var wpfAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "WindowsBase", "System.Xaml", "PresentationCore", "PresentationFramework",
        "PresentationUI", "ReachFramework", "System.Windows.Presentation",
        "System.Windows.Controls.Ribbon", "System.Windows.Input.Manipulations",
        "WindowsFormsIntegration", "UIAutomationTypes", "UIAutomationProvider",
        "UIAutomationClient", "UIAutomationClientSideProviders",
        "PresentationFramework.Aero", "PresentationFramework.Aero2",
        "PresentationFramework.AeroLite", "PresentationFramework.Classic",
        "PresentationFramework.Fluent", "PresentationFramework.Luna",
        "PresentationFramework.Royale", "DirectWriteForwarder",
        // Official package also includes these:
        "System.Printing",
    };

    // Get official DLL list (filter to WPF-relevant only)
    var officialDlls = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    foreach (var dll in Directory.GetFiles(refDir, "*.dll"))
    {
        var name = Path.GetFileNameWithoutExtension(dll);
        if (wpfAssemblies.Contains(name))
        {
            officialDlls[name] = new FileInfo(dll).Length;
        }
    }

    // Get our DLL list
    var ourDlls = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    foreach (var dll in Directory.GetFiles(ourDir, "*.dll"))
    {
        var name = Path.GetFileNameWithoutExtension(dll);
        if (wpfAssemblies.Contains(name))
        {
            ourDlls[name] = new FileInfo(dll).Length;
        }
    }
    Log.Info($"  Official WPF ref assemblies: {officialDlls.Count}");
    Log.Info($"  Our built assemblies:        {ourDlls.Count}");
    Log.Info("");

    // Missing: in official but not in ours
    var missing = officialDlls.Keys.Except(ourDlls.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
    var extra = ourDlls.Keys.Except(officialDlls.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
    var common = officialDlls.Keys.Intersect(ourDlls.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

    // Size comparison (flag if size differs by >50% — ref vs impl naturally differ)
    var sizeWarnings = new List<(string Name, long Official, long Ours)>();
    foreach (var name in common)
    {
        var officialSize = officialDlls[name];
        var ourSize = ourDlls[name];
        if (ourSize == 0 || officialSize == 0) continue;
        var ratio = (double)Math.Min(officialSize, ourSize) / Math.Max(officialSize, ourSize);
        if (ratio < 0.3)
        {
            sizeWarnings.Add((name, officialSize, ourSize));
        }
    }

    if (missing.Count > 0)
    {
        Log.Warn($"Missing DLLs (present in official, not in ours): {missing.Count}");
        foreach (var name in missing)
        {
            Log.Warn($"  - {name}.dll");
        }
    }

    if (extra.Count > 0)
    {
        Log.Info($"Extra DLLs (present in ours, not in official ref): {extra.Count}");
        foreach (var name in extra)
        {
            Log.Info($"  + {name}.dll");
        }
    }

    if (sizeWarnings.Count > 0)
    {
        Log.Warn($"Size discrepancies (>70% difference): {sizeWarnings.Count}");
        foreach (var (name, official, ours) in sizeWarnings)
        {
            Log.Warn($"  ~ {name}.dll: official={official / 1024.0:F0}KB, ours={ours / 1024.0:F0}KB");
        }
    }

    Log.Info("");
    if (missing.Count == 0 && sizeWarnings.Count == 0)
    {
        Log.Info("Comparison passed: no missing DLLs, no major size discrepancies");
        return 0;
    }

    Log.Warn("Comparison found issues — review the warnings above");
    // In build pipeline (reportOnly), don't fail the build for comparison issues
    return reportOnly ? 0 : 1;
}

// ===========================================================
// Clean mode — equivalent to `git clean -xdf` but tolerant of
// locked files (Visual Studio .vs/ cache, etc.)
// ===========================================================

static void RunClean(string repoRoot, string artifactsDir, string builderOutputDir)
{
    var deletedDirs = 0;
    var skippedDirs = 0;
    var deletedFiles = 0;
    var skippedFiles = 0;

    // 1. Delete artifacts/ directory entirely
    Log.Step("Cleaning artifacts/ ...");
    if (Directory.Exists(artifactsDir))
    {
        (deletedDirs, skippedDirs) = DeleteDirectoryRecursive(artifactsDir);
        Log.Info($"  artifacts/: deleted {deletedDirs} dirs, skipped {skippedDirs} locked");
    }
    else
    {
        Log.Info("  artifacts/ does not exist, skipping");
    }

    // 2. Recursively delete all bin/ and obj/ directories under src/
    Log.Step("Cleaning bin/ and obj/ under src/ ...");
    var srcDir = Path.Join(repoRoot, "src");
    if (Directory.Exists(srcDir))
    {
        var (d, s) = CleanNamedDirectories(srcDir, new[] { "bin", "obj" });
        deletedDirs += d;
        skippedDirs += s;
    }

    // Also clean bin/obj under Demo/ and eng/ (but NOT eng/Builder/bin)
    foreach (var sub in new[] { "Demo", "cycle-breakers" })
    {
        var subDir = Path.Join(repoRoot, sub);
        if (Directory.Exists(subDir))
        {
            var (d, s) = CleanNamedDirectories(subDir, new[] { "bin", "obj" });
            deletedDirs += d;
            skippedDirs += s;
        }
    }

    // 3. Delete .vs/ directory (Visual Studio cache — frequently locked)
    Log.Step("Cleaning .vs/ ...");
    var vsDir = Path.Join(repoRoot, ".vs");
    if (Directory.Exists(vsDir))
    {
        var (d, s) = DeleteDirectoryRecursive(vsDir);
        deletedDirs += d;
        skippedDirs += s;
        Log.Info($"  .vs/: deleted {d} dirs, skipped {s} locked");
    }
    else
    {
        Log.Info("  .vs/ does not exist, skipping");
    }

    // 4. Delete stray log files in repo root (*.log)
    Log.Step("Cleaning stray .log files in repo root ...");
    foreach (var logFile in Directory.GetFiles(repoRoot, "*.log"))
    {
        try
        {
            File.Delete(logFile);
            deletedFiles++;
        }
        catch
        {
            skippedFiles++;
        }
    }

    Log.Info("");
    Log.Info("=== Clean summary ===");
    Log.Info($"  Directories deleted: {deletedDirs}");
    Log.Info($"  Directories skipped (locked): {skippedDirs}");
    Log.Info($"  Files deleted: {deletedFiles}");
    Log.Info($"  Files skipped (locked): {skippedFiles}");
    if (skippedDirs > 0 || skippedFiles > 0)
    {
        Log.Warn("Some files/directories were locked (likely by Visual Studio).");
        Log.Warn("Close Visual Studio and re-run 'clean' for a fully clean state.");
    }
}

/// <summary>
/// Recursively delete a directory tree, tolerating locked files.
/// Returns (deletedDirCount, skippedDirCount).
/// </summary>
static (int deleted, int skipped) DeleteDirectoryRecursive(string path)
{
    var deleted = 0;
    var skipped = 0;

    // First recurse into subdirectories
    string[] subDirs;
    try
    {
        subDirs = Directory.GetDirectories(path);
    }
    catch (UnauthorizedAccessException)
    {
        Log.Warn($"  Cannot access: {path}");
        return (0, 1);
    }
    catch (IOException)
    {
        Log.Warn($"  IO error accessing: {path}");
        return (0, 1);
    }

    foreach (var subDir in subDirs)
    {
        var (d, s) = DeleteDirectoryRecursive(subDir);
        deleted += d;
        skipped += s;
    }

    // Delete files in this directory
    string[] files;
    try
    {
        files = Directory.GetFiles(path);
    }
    catch (UnauthorizedAccessException)
    {
        skipped++;
        return (deleted, skipped);
    }
    catch (IOException)
    {
        skipped++;
        return (deleted, skipped);
    }

    foreach (var file in files)
    {
        try
        {
            File.Delete(file);
        }
        catch (UnauthorizedAccessException)
        {
            // File is locked — skip it
        }
        catch (IOException)
        {
            // File is in use — skip it
        }
    }

    // Try to delete the now-empty directory
    try
    {
        Directory.Delete(path, recursive: false);
        deleted++;
    }
    catch (UnauthorizedAccessException)
    {
        skipped++;
    }
    catch (IOException)
    {
        skipped++;
    }

    return (deleted, skipped);
}

/// <summary>
/// Find and delete all directories named <paramref name="namesToClean"/>
/// (e.g. "bin", "obj") under <paramref name="rootDir"/> recursively.
/// </summary>
static (int deleted, int skipped) CleanNamedDirectories(string rootDir, string[] namesToClean)
{
    var deleted = 0;
    var skipped = 0;
    var nameSet = new HashSet<string>(namesToClean, StringComparer.OrdinalIgnoreCase);

    // Use a stack to walk the tree without recursing into deleted directories
    var stack = new Stack<string>();
    stack.Push(rootDir);

    while (stack.Count > 0)
    {
        var current = stack.Pop();

        string[] entries;
        try
        {
            entries = Directory.GetDirectories(current);
        }
        catch (UnauthorizedAccessException)
        {
            continue;
        }
        catch (IOException)
        {
            continue;
        }

        foreach (var entry in entries)
        {
            var dirName = Path.GetFileName(entry);
            if (nameSet.Contains(dirName))
            {
                var (d, s) = DeleteDirectoryRecursive(entry);
                deleted += d;
                skipped += s;
            }
            else
            {
                stack.Push(entry);
            }
        }
    }

    Log.Info($"  Deleted {deleted} directories, skipped {skipped} locked");
    return (deleted, skipped);
}

static void CleanArtifacts(string artifactsDir)
{
    if (!Directory.Exists(artifactsDir))
    {
        Log.Info("artifacts does not exist, skipping cleanup");
        return;
    }

    // Delete bin and obj first (may be partially locked)
    foreach (var subDir in new[] { "bin", "obj" })
    {
        var path = Path.Join(artifactsDir, subDir);
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (UnauthorizedAccessException)
            {
                Log.Warn($"Cannot delete {subDir} (file is locked, skipping)");
            }
            catch (IOException)
            {
                Log.Warn($"Cannot delete {subDir} (file is in use, skipping)");
            }
        }
    }

    // Try to delete loose files in the artifacts root
    try
    {
        foreach (var file in Directory.GetFiles(artifactsDir))
        {
            try { File.Delete(file); } catch { /* skip locked files */ }
        }
    }
    catch { /* ignore */ }

    Log.Info("artifacts cleanup complete");
}

static HashSet<string> GetRuntimeAssemblyNames() => new(StringComparer.OrdinalIgnoreCase)
{
    "WindowsBase",
    "System.Xaml",
    "PresentationCore",
    "PresentationFramework",
    "PresentationUI",
    "ReachFramework",
    "System.Windows.Presentation",
    "System.Windows.Controls.Ribbon",
    "System.Windows.Input.Manipulations",
    "WindowsFormsIntegration",
    "UIAutomationTypes",
    "UIAutomationProvider",
    "UIAutomationClient",
    "UIAutomationClientSideProviders",
    "PresentationFramework.Aero",
    "PresentationFramework.Aero2",
    "PresentationFramework.AeroLite",
    "PresentationFramework.Classic",
    "PresentationFramework.Fluent",
    "PresentationFramework.Luna",
    "PresentationFramework.Royale",
    "DirectWriteForwarder",
};

static Dictionary<string, string> CollectReferenceDlls(string artifactsDir)
{
    var binDir = Path.Join(artifactsDir, "bin");
    if (!Directory.Exists(binDir))
        return [];

    var wantedDlls = GetRuntimeAssemblyNames();
    wantedDlls.Remove("DirectWriteForwarder");
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var projectDir in Directory.GetDirectories(binDir, "*-ref"))
    {
        var assemblyName = Path.GetFileName(projectDir)[..^"-ref".Length];
        if (!wantedDlls.Contains(assemblyName))
            continue;

        foreach (var dllDir in new[]
        {
            Path.Join(projectDir, "x64", "Debug", "net8.0"),
            Path.Join(projectDir, "AnyCPU", "Debug", "net8.0"),
            Path.Join(projectDir, "Any CPU", "Debug", "net8.0"),
            Path.Join(projectDir, "Debug", "net8.0"),
        })
        {
            if (!Directory.Exists(dllDir)) continue;

            var dllPath = Path.Join(dllDir, $"{assemblyName}.dll");
            if (File.Exists(dllPath))
            {
                result[Path.GetFileName(dllPath)] = dllPath;
                break;
            }
        }
    }

    return result;
}

static Dictionary<string, string> CollectRuntimeDlls(string artifactsDir, string platform)
{
    var binDir = Path.Join(artifactsDir, "bin");
    if (!Directory.Exists(binDir))
        return [];

    var wantedDlls = GetRuntimeAssemblyNames();
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var projectDir in Directory.GetDirectories(binDir))
    {
        var dirName = Path.GetFileName(projectDir);
        if (dirName.EndsWith("-ref", StringComparison.OrdinalIgnoreCase)) continue;
        if (dirName.Contains("-api-cycle", StringComparison.OrdinalIgnoreCase)) continue;
        if (dirName.Contains("-impl-cycle", StringComparison.OrdinalIgnoreCase)) continue;
        if (!wantedDlls.Contains(dirName)) continue;

        var platformCandidates = platform == "x86" ? new[] { "x86", "Win32" } : new[] { platform };
        foreach (var platformCandidate in platformCandidates)
        {
            foreach (var dllDir in new[]
            {
                Path.Join(projectDir, platformCandidate, "Debug", "net8.0"),
                Path.Join(projectDir, platformCandidate, "Debug"),
                Path.Join(projectDir, "Debug", "net8.0"),
                Path.Join(projectDir, "Debug"),
            })
            {
                if (!Directory.Exists(dllDir)) continue;

                var dllPath = Path.Join(dllDir, $"{dirName}.dll");
                if (File.Exists(dllPath))
                {
                    result[Path.GetFileName(dllPath)] = dllPath;
                    break;
                }
            }
        }
    }

    return result;
}

static Dictionary<string, string> ReadPackagePaths(string builderOutputDir)
{
    var pathsFile = Path.Join(builderOutputDir, "PackagePaths.txt");
    if (!File.Exists(pathsFile))
        throw new InvalidOperationException($"Package paths file not found: {pathsFile}; build the Builder project first to resolve NuGet package paths");

    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in File.ReadAllLines(pathsFile))
    {
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
            result[parts[0].Trim()] = parts[1].Trim();
    }

    if (result.Count == 0)
        throw new InvalidOperationException("Package paths file is empty; please check NuGet package references");

    return result;
}

static void CopyNativeDllsFromPackage(Dictionary<string, string> packagePaths, string rid, string destDir)
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

static void CopyIjwHostFromPackage(Dictionary<string, string> packagePaths, string rid, string destDir)
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

static string GenerateNuspec(
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

static void GenerateBuildTransitiveTargets(string stagingDir)
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

static void ValidatePackageAssets(string stagingDir)
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

static void RequirePackageFile(string path)
{
    if (!File.Exists(path))
        throw new InvalidOperationException($"Required package asset not found: {path}");
}

static string PackNuGet(string nuspecPath, string outputDir)
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

    var result = RunProcess("dotnet", $"pack \"{tempProj}\" --output \"{outputDir}\"", packDir);

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

static ProcessResult RunProcess(string fileName, string arguments, string workingDirectory, TimeSpan? timeout = null)
{
    var startTime = Stopwatch.GetTimestamp();
    var psi = new ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    using var process = Process.Start(psi)
        ?? throw new InvalidOperationException($"Failed to start process: {fileName}");
    var outputTask = process.StandardOutput.ReadToEndAsync();
    var errorTask = process.StandardError.ReadToEndAsync();
    var exited = timeout is null
        ? process.WaitForExit(int.MaxValue)
        : process.WaitForExit((int) timeout.Value.TotalMilliseconds);
    if (!exited)
    {
        process.Kill(entireProcessTree: true);
        process.WaitForExit();
        System.Threading.Tasks.Task.WaitAll(outputTask, errorTask);
        throw new TimeoutException($"Process timed out after {timeout!.Value.TotalSeconds:F0} seconds: {fileName} {arguments}");
    }

    System.Threading.Tasks.Task.WaitAll(outputTask, errorTask);

    return new ProcessResult(process.ExitCode, outputTask.Result + errorTask.Result, Stopwatch.GetElapsedTime(startTime));
}

// ===========================================================
// Simple logging utility
// ===========================================================
internal static class Log
{
    public static void Step(string message) => Console.WriteLine($"\n--- {message}");
    public static void Info(string message) => Console.WriteLine($"    {message}");
    public static void Warn(string message) => Console.WriteLine($"    [WARN] {message}");
    public static void Error(string message) => Console.Error.WriteLine($"    [ERROR] {message}");
}

internal readonly record struct ProcessResult(int ExitCode, string Output, TimeSpan Elapsed);

internal sealed record PackageDependency(string Id, string Version);

internal sealed record PackageTestProject(string Name, string ProjectPath, IReadOnlyList<string> TargetFrameworks);

internal static class PackageMetadata
{
    public static IReadOnlyList<string> TargetFrameworks { get; } = ["net8.0", "net9.0"];
}

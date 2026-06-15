using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

// ===========================================================
// DotNetCampus.WpfLib Builder — Build driver + NuGet packaging tool
// Usage: dotnet run --project eng\Builder\Builder.csproj
// ===========================================================

var repoRoot = FindRepoRoot();
var artifactsDir = Path.Join(repoRoot, "artifacts");
var builderOutputDir = Path.Join(repoRoot, "eng", "Builder", "bin");
var stagingDir = Path.Join(builderOutputDir, "staging");
var nupkgOutputDir = Path.Join(builderOutputDir, "nupkg");

var startTime = Stopwatch.GetTimestamp();

Log.Info("=== DotNetCampus.WpfLib Builder ===");
Log.Info($"Repo root: {repoRoot}");

// ---- Step 1: Clean artifacts ----
Log.Step("Cleaning artifacts folder...");
CleanArtifacts(artifactsDir);

// ---- Step 2: Clean staging directory ----
Log.Step("Cleaning staging directory...");
if (Directory.Exists(stagingDir))
    Directory.Delete(stagingDir, recursive: true);

// ---- Step 3: Build projects ----
Log.Step("Building projects (x64)...");
var srcDir = Path.Join(repoRoot, "src", "Microsoft.DotNet.Wpf", "src");
var msbuildArgs = $"-restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly";

// Build in dependency order: build dependencies first, then their dependents
var projectsToBuild = new[]
{
    // PresentationBuildTasks must be built first — it is the MSBuild task assembly for XAML markup compilation (MarkupCompilePass1)
    Path.Join(srcDir, "PresentationBuildTasks", "PresentationBuildTasks.csproj"),
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
foreach (var projectPath in projectsToBuild)
{
    if (!File.Exists(projectPath))
    {
        Log.Warn($"Project not found, skipping: {projectPath}");
        continue;
    }

    var projectName = Path.GetFileNameWithoutExtension(projectPath);
    Log.Info($"  Building {projectName}...");

    // PresentationBuildTasks is an MSBuild task assembly; .NET Framework MSBuild requires net472 target
    var extraArgs = projectName == "PresentationBuildTasks" ? " /p:TargetFramework=net472" : "";
    var result = RunProcess("msbuild", $"\"{projectPath}\" {msbuildArgs}{extraArgs}", repoRoot);
    if (result.ExitCode != 0)
    {
        Log.Error($"  Build failed: {projectName}");
        failedProjects.Add(projectName);
        // Continue building remaining projects; do not abort immediately
    }
}

if (failedProjects.Count > 0)
{
    Log.Warn($"The following projects failed to build (their DLLs will be skipped): {string.Join(", ", failedProjects)}");
}
else
{
    Log.Info("All projects built successfully");
}

// ---- Step 4: Collect managed DLLs ----
Log.Step("Collecting managed DLLs...");
var managedDlls = CollectManagedDlls(artifactsDir);
if (managedDlls.Count == 0)
{
    Log.Error("No managed DLLs found; please check build artifacts");
    return 1;
}

var libDir = Path.Join(stagingDir, "lib", "net8.0");
Directory.CreateDirectory(libDir);
foreach (var (name, sourcePath) in managedDlls)
{
    var destPath = Path.Join(libDir, name);
    File.Copy(sourcePath, destPath, overwrite: true);
    Log.Info($"  lib/net8.0/{name}");
}

// ---- Step 5: Collect native DLLs ----
Log.Step("Collecting native DLLs...");
var packagePaths = ReadPackagePaths(builderOutputDir);
var runtimesDir = Path.Join(stagingDir, "runtimes");
CopyNativeDllsFromPackage(packagePaths, "win-x64", Path.Join(runtimesDir, "win-x64", "native"));
CopyNativeDllsFromPackage(packagePaths, "win-x86", Path.Join(runtimesDir, "win-x86", "native"));

// ---- Step 6: Generate .nuspec and pack ----
Log.Step("Generating .nuspec and packing...");
var version = "1.0.0";
var nuspecPath = GenerateNuspec(stagingDir, version);
var nupkgPath = PackNuGet(nuspecPath, nupkgOutputDir);

// ---- Done ----
var elapsed = Stopwatch.GetElapsedTime(startTime);
Log.Info("========================================");
Log.Info($"Build complete! Elapsed: {elapsed.TotalSeconds:F1}s");
Log.Info($"NuGet package: {nupkgPath}");
return 0;

// ===========================================================
// Helper methods
// ===========================================================

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

static Dictionary<string, string> CollectManagedDlls(string artifactsDir)
{
    var binDir = Path.Join(artifactsDir, "bin");
    if (!Directory.Exists(binDir))
        return [];

    var wantedDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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

    var excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Builder", "Docs", "WpfDemo",
    };

    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var projectDir in Directory.GetDirectories(binDir))
    {
        var dirName = Path.GetFileName(projectDir);
        if (excludedDirs.Contains(dirName)) continue;
        if (dirName.EndsWith("-ref", StringComparison.OrdinalIgnoreCase)) continue;
        if (dirName.Contains("-api-cycle", StringComparison.OrdinalIgnoreCase)) continue;
        if (dirName.Contains("-impl-cycle", StringComparison.OrdinalIgnoreCase)) continue;

        // Build uses /p:Platform=x64, so output path is artifacts\bin\<ProjectName>\x64\Debug\net8.0\
        // Also compatible with Any CPU path: artifacts\bin\<ProjectName>\Debug\net8.0\
        foreach (var candidate in new[] { "x64", "" })
        {
            var dllDir = string.IsNullOrEmpty(candidate)
                ? Path.Join(projectDir, "Debug", "net8.0")
                : Path.Join(projectDir, candidate, "Debug", "net8.0");

            if (!Directory.Exists(dllDir)) continue;

            foreach (var dllPath in Directory.GetFiles(dllDir, "*.dll"))
            {
                var dllName = Path.GetFileNameWithoutExtension(dllPath);
                if (wantedDlls.Contains(dllName))
                {
                    result[Path.GetFileName(dllPath)] = dllPath;
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

static string GenerateNuspec(string stagingDir, string version)
{
    var managedDir = Path.Join(stagingDir, "lib", "net8.0");
    var managedFiles = Directory.Exists(managedDir)
        ? Directory.GetFiles(managedDir, "*.dll").Select(Path.GetFileName).OrderBy(x => x).ToList()
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
    sb.AppendLine("  </metadata>");
    sb.AppendLine("  <files>");

    foreach (var file in managedFiles)
    {
        sb.AppendLine($"    <file src=\"lib\\net8.0\\{file}\" target=\"lib\\net8.0\\{file}\" />");
    }

    // Native DLL (win-x64)
    var winX64NativeDir = Path.Join(stagingDir, "runtimes", "win-x64", "native");
    if (Directory.Exists(winX64NativeDir))
    {
        foreach (var file in Directory.GetFiles(winX64NativeDir, "*.dll"))
        {
            var fileName = Path.GetFileName(file);
            sb.AppendLine($"    <file src=\"runtimes\\win-x64\\native\\{fileName}\" target=\"runtimes\\win-x64\\native\\{fileName}\" />");
        }
    }

    // Native DLL (win-x86)
    var winX86NativeDir = Path.Join(stagingDir, "runtimes", "win-x86", "native");
    if (Directory.Exists(winX86NativeDir))
    {
        foreach (var file in Directory.GetFiles(winX86NativeDir, "*.dll"))
        {
            var fileName = Path.GetFileName(file);
            sb.AppendLine($"    <file src=\"runtimes\\win-x86\\native\\{fileName}\" target=\"runtimes\\win-x86\\native\\{fileName}\" />");
        }
    }

    sb.AppendLine("  </files>");
    sb.AppendLine("</package>");

    var nuspecContent = sb.ToString();
    var nuspecPath = Path.Join(stagingDir, "DotNetCampus.WpfLib.nuspec");
    File.WriteAllText(nuspecPath, nuspecContent);
    Log.Info($"  .nuspec generated: {nuspecPath}");
    return nuspecPath;
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

static ProcessResult RunProcess(string fileName, string arguments, string workingDirectory)
{
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

    using var process = Process.Start(psi)!;
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    return new ProcessResult(process.ExitCode, output + error);
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

internal readonly record struct ProcessResult(int ExitCode, string Output);

using System.Diagnostics;

namespace WpfReorganize.Builder;

internal static class BuildService
{
    public static int Run(BuilderContext context, string version)
    {
var startTime = Stopwatch.GetTimestamp();

Log.Info($"=== {PackageMetadata.Id} Builder ===");
Log.Info($"Repo root: {context.RepoRoot}");

// ---- Step 1: Clean artifacts ----
Log.Step("Cleaning artifacts folder...");
CleanService.CleanArtifacts(context.ArtifactsDir);
Directory.CreateDirectory(context.BuildLogsDir);

// ---- Step 2: Clean staging directory ----
Log.Step("Cleaning staging directory...");
if (Directory.Exists(context.StagingDir))
    Directory.Delete(context.StagingDir, recursive: true);

// ---- Step 3: Build projects ----
Log.Step("Building projects (x64 + x86)...");
var msbuildExe = MsBuildService.FindMsBuild();
Log.Info($"  MSBuild: {msbuildExe}");

// Build in dependency order: build dependencies first, then their dependents
var projectsToBuild = new[]
{
    Path.Join(context.SourceDir, "WindowsBase", "WindowsBase.csproj"),
    Path.Join(context.SourceDir, "System.Xaml", "System.Xaml.csproj"),
    Path.Join(context.SourceDir, "UIAutomation", "UIAutomationTypes", "UIAutomationTypes.csproj"),
    Path.Join(context.SourceDir, "UIAutomation", "UIAutomationProvider", "UIAutomationProvider.csproj"),
    Path.Join(context.SourceDir, "DirectWriteForwarder", "DirectWriteForwarder.vcxproj"),
    Path.Join(context.SourceDir, "PresentationCore", "PresentationCore.csproj"),
    Path.Join(context.SourceDir, "UIAutomation", "UIAutomationClient", "UIAutomationClient.csproj"),
    Path.Join(context.SourceDir, "UIAutomation", "UIAutomationClientSideProviders", "UIAutomationClientSideProviders.csproj"),
    Path.Join(context.SourceDir, "PresentationFramework", "PresentationFramework.csproj"),
    Path.Join(context.SourceDir, "ReachFramework", "ReachFramework.csproj"),
    Path.Join(context.SourceDir, "System.Windows.Presentation", "System.Windows.Presentation.csproj"),
    Path.Join(context.SourceDir, "System.Windows.Input.Manipulations", "System.Windows.Input.Manipulations.csproj"),
    Path.Join(context.SourceDir, "PresentationUI", "PresentationUI.csproj"),
    Path.Join(context.SourceDir, "System.Windows.Controls.Ribbon", "System.Windows.Controls.Ribbon.csproj"),
    Path.Join(context.SourceDir, "WindowsFormsIntegration", "WindowsFormsIntegration.csproj"),
    Path.Join(context.SourceDir, "Themes", "PresentationFramework.Aero", "PresentationFramework.Aero.csproj"),
    Path.Join(context.SourceDir, "Themes", "PresentationFramework.Aero2", "PresentationFramework.Aero2.csproj"),
    Path.Join(context.SourceDir, "Themes", "PresentationFramework.AeroLite", "PresentationFramework.AeroLite.csproj"),
    Path.Join(context.SourceDir, "Themes", "PresentationFramework.Classic", "PresentationFramework.Classic.csproj"),
    Path.Join(context.SourceDir, "Themes", "PresentationFramework.Fluent", "PresentationFramework.Fluent.csproj"),
    Path.Join(context.SourceDir, "Themes", "PresentationFramework.Luna", "PresentationFramework.Luna.csproj"),
    Path.Join(context.SourceDir, "Themes", "PresentationFramework.Royale", "PresentationFramework.Royale.csproj"),
};

var failedProjects = new List<string>();

// PresentationBuildTasks is an MSBuild task assembly rather than a runtime asset.
// Its lookup path includes WpfNativePlatform, so prebuild one copy per runtime architecture.
var presentationBuildTasksPath = Path.Join(context.SourceDir, "PresentationBuildTasks", "PresentationBuildTasks.csproj");
if (!File.Exists(presentationBuildTasksPath))
{
    Log.Error($"PresentationBuildTasks project not found: {presentationBuildTasksPath}");
    return 1;
}

foreach (var platform in new[] { "x64", "x86" })
{
    var projectName = "PresentationBuildTasks";
    var logPath = MsBuildService.GetBuildLogPath(context.BuildLogsDir, projectName, platform);
    var arguments = $"\"{presentationBuildTasksPath}\" -restore /p:Configuration=Debug /p:Platform={platform} /p:TargetFramework=net472 /m:1 /nr:false /v:minimal /clp:ErrorsOnly{MsBuildService.GetFileLoggerArguments(logPath)}";
    var presentationBuildTasksResult = ProcessRunner.Run(
        msbuildExe,
        arguments,
        context.RepoRoot);
    if (presentationBuildTasksResult.ExitCode != 0)
    {
        MsBuildService.LogBuildFailure(projectName, platform, msbuildExe, arguments, context.RepoRoot, logPath, presentationBuildTasksResult);
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

        var logPath = MsBuildService.GetBuildLogPath(context.BuildLogsDir, projectName, platform);
        var arguments = $"\"{projectPath}\" -restore /p:Configuration=Debug /p:Platform={projectPlatform} /p:DebugSymbols=true /p:DebugType=portable /p:UsePrebuiltPresentationBuildTasks=true /p:BuildPresentationBuildTasksOnDemand=false /m:1 /nr:false /v:minimal /clp:ErrorsOnly{MsBuildService.GetFileLoggerArguments(logPath)}";
        var result = ProcessRunner.Run(
            msbuildExe,
            arguments,
            context.RepoRoot);
        if (result.ExitCode != 0)
        {
            MsBuildService.LogBuildFailure(projectName, platform, msbuildExe, arguments, context.RepoRoot, logPath, result);
            failedProjects.Add($"{projectName} ({platform})");
            // Continue building remaining projects; do not abort immediately
        }
    }
}

if (failedProjects.Count > 0)
{
    Log.Warn($"The following projects failed to build (their DLLs will be skipped): {string.Join(", ", failedProjects)}");
    Log.Warn($"Diagnostic MSBuild logs: {context.BuildLogsDir}");
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
var referenceDlls = AssemblyCollector.CollectReferenceDlls(context.RepoRoot, context.ArtifactsDir);
if (referenceDlls.Count == 0)
{
    Log.Error("No reference assemblies found; please check build artifacts");
    return 1;
}

var refDir = Path.Join(context.StagingDir, "ref", "net8.0");
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
    var runtimeDlls = AssemblyCollector.CollectRuntimeDlls(context.RepoRoot, context.ArtifactsDir, platform);
    if (runtimeDlls.Count == 0)
    {
        Log.Error($"No runtime assemblies found for {rid}; please check build artifacts");
        return 1;
    }

    var runtimeLibDir = Path.Join(context.StagingDir, "runtimes", rid, "lib", "net8.0");
    Directory.CreateDirectory(runtimeLibDir);
    foreach (var (name, sourcePath) in runtimeDlls)
    {
        var destPath = Path.Join(runtimeLibDir, name);
        File.Copy(sourcePath, destPath, overwrite: true);
        Log.Info($"  runtimes/{rid}/lib/net8.0/{name}");

        var pdbSourcePath = AssemblyCollector.GetPdbPath(sourcePath);
        if (pdbSourcePath is not null)
        {
            var pdbName = Path.GetFileName(pdbSourcePath);
            File.Copy(pdbSourcePath, Path.Join(runtimeLibDir, pdbName), overwrite: true);
            Log.Info($"  runtimes/{rid}/lib/net8.0/{pdbName} (symbols)");
        }
    }
}

// ---- Step 5: Collect native DLLs ----
Log.Step("Collecting native DLLs...");
var packagePaths = NuGetPackageService.ReadPackagePaths(context.BuilderOutputDir);
var runtimesDir = Path.Join(context.StagingDir, "runtimes");
NuGetPackageService.CopyNativeDllsFromPackage(packagePaths, "win-x64", Path.Join(runtimesDir, "win-x64", "native"));
NuGetPackageService.CopyNativeDllsFromPackage(packagePaths, "win-x86", Path.Join(runtimesDir, "win-x86", "native"));
NuGetPackageService.CopyIjwHostFromPackage(packagePaths, "win-x64", Path.Join(runtimesDir, "win-x64", "native"));
NuGetPackageService.CopyIjwHostFromPackage(packagePaths, "win-x86", Path.Join(runtimesDir, "win-x86", "native"));

// ---- Step 6: Generate .nuspec and pack ----
Log.Step("Generating .nuspec and packing...");
Log.Info($"  Package version: {version}");
NuGetPackageService.GenerateBuildTransitiveTargets(context.StagingDir);
try
{
    NuGetPackageService.ValidatePackageAssets(context.StagingDir);
}
catch (InvalidOperationException exception)
{
    Log.Error(exception.Message);
    return 1;
}
var runtimePackageDependencies = NuGetPackageService.ReadRuntimePackageDependencies(context.RepoRoot);
var nuspecPath = NuGetPackageService.GenerateNuspec(context.StagingDir, version, runtimePackageDependencies);
var symbolNuspecPath = NuGetPackageService.GenerateSymbolNuspec(context.StagingDir, version);
var nupkgPath = NuGetPackageService.PackNuGet(nuspecPath, context.NupkgOutputDir);
var snupkgPath = NuGetPackageService.PackSymbolNuGet(symbolNuspecPath, context.NupkgOutputDir);

// ---- Step 7: Compare against official package ----
Log.Step("Comparing against official Microsoft.WindowsDesktop.App.Ref...");
CompareService.Run(context, reportOnly: true);

var elapsed = Stopwatch.GetElapsedTime(startTime);
Log.Info("========================================");
Log.Info($"Build complete! Elapsed: {elapsed.TotalSeconds:F1}s");
Log.Info($"NuGet package: {nupkgPath}");
Log.Info($"NuGet symbol package: {snupkgPath}");
return failedProjects.Count > 0 ? 2 : 0;

    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

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
// ===========================================================

var repoRoot = FindRepoRoot();
var artifactsDir = Path.Join(repoRoot, "artifacts");
var builderOutputDir = Path.Join(repoRoot, "eng", "Builder", "bin");
var stagingDir = Path.Join(builderOutputDir, "staging");
var nupkgOutputDir = Path.Join(builderOutputDir, "nupkg");

// ---- Parse command-line arguments ----
var cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToList();
var command = cmdArgs.FirstOrDefault(a => !a.StartsWith("--"))?.ToLowerInvariant();
var versionArg = cmdArgs.SkipWhile(a => a != "--version").Skip(1).FirstOrDefault();
var version = string.IsNullOrEmpty(versionArg) ? "1.0.0" : versionArg;

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
    var presentationBuildTasksResult = RunProcess(
        msbuildExe,
        $"\"{presentationBuildTasksPath}\" -restore /p:Configuration=Debug /p:Platform={platform} /p:TargetFramework=net472 /m:1 /nr:false /v:minimal /clp:ErrorsOnly",
        repoRoot);
    if (presentationBuildTasksResult.ExitCode != 0)
    {
        Log.Error($"  Build failed: PresentationBuildTasks ({platform})");
        Log.Error(presentationBuildTasksResult.Output);
        failedProjects.Add($"PresentationBuildTasks ({platform})");
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

        var result = RunProcess(
            msbuildExe,
            $"\"{projectPath}\" -restore /p:Configuration=Debug /p:Platform={projectPlatform} /p:UsePrebuiltPresentationBuildTasks=true /p:BuildPresentationBuildTasksOnDemand=false /m:1 /nr:false /v:minimal /clp:ErrorsOnly",
            repoRoot);
        if (result.ExitCode != 0)
        {
            Log.Error($"  Build failed: {projectName} ({platform})");
            Log.Error(result.Output);
            failedProjects.Add($"{projectName} ({platform})");
            // Continue building remaining projects; do not abort immediately
        }
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

// ---- Step 6: Generate .nuspec and pack ----
Log.Step("Generating .nuspec and packing...");
Log.Info($"  Package version: {version}");
try
{
    ValidatePackageAssets(stagingDir);
}
catch (InvalidOperationException exception)
{
    Log.Error(exception.Message);
    return 1;
}
var nuspecPath = GenerateNuspec(stagingDir, version);
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
        foreach (var dllDir in new[]
        {
            Path.Join(projectDir, "x64", "Debug", "net8.0"),
            Path.Join(projectDir, "AnyCPU", "Debug", "net8.0"),
            Path.Join(projectDir, "Any CPU", "Debug", "net8.0"),
            Path.Join(projectDir, "Debug", "net8.0"),
        })
        {
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

    sb.AppendLine("  </files>");
    sb.AppendLine("</package>");

    var nuspecContent = sb.ToString();
    var nuspecPath = Path.Join(stagingDir, "DotNetCampus.WpfLib.nuspec");
    File.WriteAllText(nuspecPath, nuspecContent);
    Log.Info($"  .nuspec generated: {nuspecPath}");
    return nuspecPath;
}

static void ValidatePackageAssets(string stagingDir)
{
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

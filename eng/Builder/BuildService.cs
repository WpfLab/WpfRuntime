using System.Diagnostics;

namespace WpfReorganize.Builder;

internal static class BuildService
{
    public static int Run(BuilderContext context, string version)
    {
        var startTime = Stopwatch.GetTimestamp();

        Log.Info($"=== {PackageMetadata.Id} Builder ===");
        Log.Info($"Repo root: {context.RepoRoot}");

        Log.Step("Cleaning artifacts folder...");
        CleanService.CleanArtifacts(context.ArtifactsDir);
        Directory.CreateDirectory(context.BuildLogsDir);

        Log.Step("Cleaning staging directory...");
        if (Directory.Exists(context.StagingDir))
            Directory.Delete(context.StagingDir, recursive: true);

        Log.Step("Building projects (x64 + x86)...");
        var msbuildExe = MsBuildService.FindMsBuild();
        Log.Info($"  MSBuild: {msbuildExe}");

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
        var presentationBuildTasksPath = Path.Join(context.SourceDir, "PresentationBuildTasks", "PresentationBuildTasks.csproj");
        if (!File.Exists(presentationBuildTasksPath))
        {
            Log.Error($"PresentationBuildTasks project not found: {presentationBuildTasksPath}");
            return 1;
        }

        var presentationBuildTasksBuilds = new[]
        {
            (OutputPlatform: "x64", TargetFramework: "net472"),
            (OutputPlatform: "x64", TargetFramework: "net8.0"),
            (OutputPlatform: "x86", TargetFramework: "net472"),
        };
        foreach (var build in presentationBuildTasksBuilds)
        {
            const string projectName = "PresentationBuildTasks";
            var buildName = $"{build.OutputPlatform}-{build.TargetFramework}";
            var logPath = MsBuildService.GetBuildLogPath(context.BuildLogsDir, projectName, buildName);
            var arguments = GetPresentationBuildTasksBuildArguments(
                presentationBuildTasksPath,
                build.OutputPlatform,
                build.TargetFramework,
                logPath);
            var result = ProcessRunner.Run(msbuildExe, arguments, context.RepoRoot);
            if (result.ExitCode == 0)
                continue;

            MsBuildService.LogBuildFailure(projectName, buildName, msbuildExe, arguments, context.RepoRoot, logPath, result);
            failedProjects.Add($"{projectName} ({buildName})");
        }

        if (failedProjects.Count > 0)
            return 1;

        foreach (var build in presentationBuildTasksBuilds)
        {
            var outputPath = GetPresentationBuildTasksOutputPath(
                context.ArtifactsDir,
                build.OutputPlatform,
                build.TargetFramework);
            if (File.Exists(outputPath))
                continue;

            Log.Error($"PresentationBuildTasks build succeeded but required output was not found: {outputPath}");
            return 1;
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
                var arguments = GetRuntimeBuildArguments(projectPath, projectPlatform, logPath);
                var result = ProcessRunner.Run(msbuildExe, arguments, context.RepoRoot);
                if (result.ExitCode == 0)
                    continue;

                MsBuildService.LogBuildFailure(projectName, platform, msbuildExe, arguments, context.RepoRoot, logPath, result);
                failedProjects.Add($"{projectName} ({platform})");
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
            File.Copy(sourcePath, Path.Join(refDir, name), overwrite: true);
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
                File.Copy(sourcePath, Path.Join(runtimeLibDir, name), overwrite: true);
                Log.Info($"  runtimes/{rid}/lib/net8.0/{name}");

                var pdbSourcePath = AssemblyCollector.GetPdbPath(sourcePath);
                if (pdbSourcePath is null)
                    continue;

                var pdbName = Path.GetFileName(pdbSourcePath);
                File.Copy(pdbSourcePath, Path.Join(runtimeLibDir, pdbName), overwrite: true);
                Log.Info($"  runtimes/{rid}/lib/net8.0/{pdbName} (symbols)");
            }
        }

        Log.Step("Collecting native DLLs...");
        var packagePaths = NuGetPackageService.ReadPackagePaths(context.BuilderOutputDir);
        var runtimesDir = Path.Join(context.StagingDir, "runtimes");
        NuGetPackageService.CopyNativeDllsFromPackage(packagePaths, "win-x64", Path.Join(runtimesDir, "win-x64", "native"));
        NuGetPackageService.CopyNativeDllsFromPackage(packagePaths, "win-x86", Path.Join(runtimesDir, "win-x86", "native"));
        NuGetPackageService.CopyIjwHostFromPackage(
            packagePaths,
            "win-x64",
            Path.Join(runtimesDir, "win-x64", "native"),
            Path.Join(runtimesDir, "win-x64", "lib", "net8.0"));
        NuGetPackageService.CopyIjwHostFromPackage(
            packagePaths,
            "win-x86",
            Path.Join(runtimesDir, "win-x86", "native"),
            Path.Join(runtimesDir, "win-x86", "lib", "net8.0"));

        Log.Step("Generating .nuspec and packing...");
        Log.Info($"  Package version: {version}");
        try
        {
            var presentationBuildTasksOutputDir = Path.GetDirectoryName(
                GetPresentationBuildTasksOutputPath(context.ArtifactsDir, "x64", "net8.0"))!;
            NuGetPackageService.CopyPresentationBuildTasks(presentationBuildTasksOutputDir, context.StagingDir);
            NuGetPackageService.GenerateBuildTransitiveFiles(context.StagingDir);
            NuGetPackageService.ValidatePackageAssets(context.StagingDir);
        }
        catch (InvalidOperationException exception)
        {
            Log.Error(exception.Message);
            return 1;
        }

        var runtimePackageDependencies = NuGetPackageService.ReadRuntimePackageDependencies(context.RepoRoot);
        var readmePath = Path.Join(context.RepoRoot, "README.md");
        var nuspecPath = NuGetPackageService.GenerateNuspec(context.StagingDir, version, runtimePackageDependencies, readmePath);
        var symbolNuspecPath = NuGetPackageService.GenerateSymbolNuspec(context.StagingDir, version);
        var nupkgPath = NuGetPackageService.PackNuGet(nuspecPath, context.NupkgOutputDir);
        var snupkgPath = NuGetPackageService.PackSymbolNuGet(symbolNuspecPath, context.NupkgOutputDir);
        var allSymbolsArchivePath = NuGetPackageService.CreateAllSymbolsArchive(context.ArtifactsDir, version, context.NupkgOutputDir);

        Log.Step("Comparing against official Microsoft.WindowsDesktop.App.Ref...");
        CompareService.Run(context, reportOnly: true);

        var elapsed = Stopwatch.GetElapsedTime(startTime);
        Log.Info("========================================");
        Log.Info($"Build complete! Elapsed: {elapsed.TotalSeconds:F1}s");
        Log.Info($"NuGet package: {nupkgPath}");
        Log.Info($"NuGet symbol package: {snupkgPath}");
        Log.Info($"All-symbols archive: {allSymbolsArchivePath}");
        return failedProjects.Count > 0 ? 2 : 0;
    }

    internal static string GetRuntimeBuildArguments(
        string projectPath,
        string platform,
        string logPath) =>
        $"\"{projectPath}\" -restore /p:Configuration=Release /p:Platform={platform} /p:DebugSymbols=true /p:DebugType=portable /p:UsePrebuiltPresentationBuildTasks=true /p:BuildPresentationBuildTasksOnDemand=false /m:1 /nr:false /v:minimal /clp:ErrorsOnly{MsBuildService.GetFileLoggerArguments(logPath)}";

    internal static string GetPresentationBuildTasksBuildArguments(
        string projectPath,
        string outputPlatform,
        string targetFramework,
        string logPath) =>
        $"\"{projectPath}\" -restore /p:Configuration=Debug /p:Platform=x64 /p:WpfNativePlatform={outputPlatform} /p:TargetFramework={targetFramework} /m:1 /nr:false /v:minimal /clp:ErrorsOnly{MsBuildService.GetFileLoggerArguments(logPath)}";

    internal static string GetPresentationBuildTasksOutputPath(
        string artifactsDir,
        string outputPlatform,
        string targetFramework) =>
        Path.Join(
            artifactsDir,
            "bin",
            "PresentationBuildTasks",
            outputPlatform,
            "Debug",
            targetFramework,
            "PresentationBuildTasks.dll");
}

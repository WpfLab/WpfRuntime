using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

// ===========================================================
// DotNetCampus.WpfLib Builder — 构建驱动 + NuGet 打包工具
// 用法: dotnet run --project eng\Builder\Builder.csproj
// ===========================================================

var repoRoot = FindRepoRoot();
var artifactsDir = Path.Join(repoRoot, "artifacts");
var builderOutputDir = Path.Join(repoRoot, "eng", "Builder", "bin");
var stagingDir = Path.Join(builderOutputDir, "staging");
var nupkgOutputDir = Path.Join(builderOutputDir, "nupkg");

var startTime = Stopwatch.GetTimestamp();

Log.Info("=== DotNetCampus.WpfLib Builder ===");
Log.Info($"仓库根目录: {repoRoot}");

// ---- 步骤 1: 清空 artifacts ----
Log.Step("清空 artifacts 文件夹...");
CleanArtifacts(artifactsDir);

// ---- 步骤 2: 清空 staging 目录 ----
Log.Step("清空 staging 目录...");
if (Directory.Exists(stagingDir))
    Directory.Delete(stagingDir, recursive: true);

// ---- 步骤 3: 构建项目 ----
Log.Step("构建项目 (x64)...");
var srcDir = Path.Join(repoRoot, "src", "Microsoft.DotNet.Wpf", "src");
var msbuildArgs = $"-restore /p:Configuration=Debug /p:Platform=x64 /m:1 /v:minimal /clp:ErrorsOnly";

// 按依赖顺序构建：先构建被依赖的项目，再构建依赖它们的项目
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
foreach (var projectPath in projectsToBuild)
{
    if (!File.Exists(projectPath))
    {
        Log.Warn($"项目不存在，跳过: {projectPath}");
        continue;
    }

    var projectName = Path.GetFileNameWithoutExtension(projectPath);
    Log.Info($"  构建 {projectName}...");
    var result = RunProcess("msbuild", $"\"{projectPath}\" {msbuildArgs}", repoRoot);
    if (result.ExitCode != 0)
    {
        Log.Error($"  构建失败: {projectName}");
        failedProjects.Add(projectName);
        // 继续构建剩余项目，不立即中止
    }
}

if (failedProjects.Count > 0)
{
    Log.Error($"以下项目构建失败: {string.Join(", ", failedProjects)}");
    return 1;
}
Log.Info("所有项目构建成功");

// ---- 步骤 4: 收集托管 DLL ----
Log.Step("收集托管 DLL...");
var managedDlls = CollectManagedDlls(artifactsDir);
if (managedDlls.Count == 0)
{
    Log.Error("未找到任何托管 DLL，请检查构建产物");
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

// ---- 步骤 5: 收集 Native DLL ----
Log.Step("收集 Native DLL...");
var packagePaths = ReadPackagePaths(builderOutputDir);
var runtimesDir = Path.Join(stagingDir, "runtimes");
CopyNativeDllsFromPackage(packagePaths, "win-x64", Path.Join(runtimesDir, "win-x64", "native"));
CopyNativeDllsFromPackage(packagePaths, "win-x86", Path.Join(runtimesDir, "win-x86", "native"));

// ---- 步骤 6: 生成 .nuspec 并打包 ----
Log.Step("生成 .nuspec 并打包...");
var version = "1.0.0";
var nuspecPath = GenerateNuspec(stagingDir, version);
var nupkgPath = PackNuGet(nuspecPath, nupkgOutputDir);

// ---- 完成 ----
var elapsed = Stopwatch.GetElapsedTime(startTime);
Log.Info("========================================");
Log.Info($"构建完成！耗时 {elapsed.TotalSeconds:F1}s");
Log.Info($"NuGet 包: {nupkgPath}");
return 0;

// ===========================================================
// 辅助方法
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
    throw new InvalidOperationException("无法找到仓库根目录（.git 目录）");
}

static void CleanArtifacts(string artifactsDir)
{
    if (!Directory.Exists(artifactsDir))
    {
        Log.Info("artifacts 不存在，跳过清理");
        return;
    }

    // 先删除 bin 和 obj（可能被部分锁定）
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
                Log.Warn($"无法删除 {subDir}（文件被锁定，跳过）");
            }
            catch (IOException)
            {
                Log.Warn($"无法删除 {subDir}（文件被占用，跳过）");
            }
        }
    }

    // 尝试删除 artifacts 根目录下的零散文件
    try
    {
        foreach (var file in Directory.GetFiles(artifactsDir))
        {
            try { File.Delete(file); } catch { /* 跳过锁定文件 */ }
        }
    }
    catch { /* 忽略 */ }

    Log.Info("artifacts 清理完成");
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

        // 构建使用 /p:Platform=x64，因此产物路径为 artifacts\bin\<ProjectName>\x64\Debug\net8.0\
        // 同时兼容 Any CPU 的路径: artifacts\bin\<ProjectName>\Debug\net8.0\
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
        throw new InvalidOperationException($"未找到包路径文件: {pathsFile}，请先构建 Builder 项目以解析 NuGet 包路径");

    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in File.ReadAllLines(pathsFile))
    {
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
            result[parts[0].Trim()] = parts[1].Trim();
    }

    if (result.Count == 0)
        throw new InvalidOperationException("包路径文件为空，请检查 NuGet 包引用");

    return result;
}

static void CopyNativeDllsFromPackage(Dictionary<string, string> packagePaths, string rid, string destDir)
{
    if (!packagePaths.TryGetValue(rid, out var packageRoot))
    {
        Log.Warn($"未找到 {rid} 的包路径");
        return;
    }

    var sourceDir = Path.Join(packageRoot, "runtimes", rid, "native");

    if (!Directory.Exists(sourceDir))
    {
        Log.Warn($"Native 源目录不存在: {sourceDir}");
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
    sb.AppendLine("    <description>WPF 自定义构建的托管程序集与 native 运行时。</description>");
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
    Log.Info($"  .nuspec 已生成: {nuspecPath}");
    return nuspecPath;
}

static string PackNuGet(string nuspecPath, string outputDir)
{
    Directory.CreateDirectory(outputDir);

    // _pack.csproj 放在独立于仓库树的位置（系统临时目录），
    // 避免继承仓库根 Directory.Build.props 中的 Arcade SDK 导入
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
        Log.Error($"打包失败: {result.Output}");
        throw new InvalidOperationException("NuGet 打包失败");
    }

    var nupkgFiles = Directory.GetFiles(outputDir, "*.nupkg");
    if (nupkgFiles.Length == 0)
        throw new InvalidOperationException("未找到生成的 .nupkg 文件");

    var nupkgPath = nupkgFiles.OrderByDescending(File.GetLastWriteTime).First();
    var fileInfo = new FileInfo(nupkgPath);
    Log.Info($"  .nupkg 已生成: {nupkgPath} ({fileInfo.Length / 1024.0:F1} KB)");
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
// 简单日志工具
// ===========================================================
internal static class Log
{
    public static void Step(string message) => Console.WriteLine($"\n--- {message}");
    public static void Info(string message) => Console.WriteLine($"    {message}");
    public static void Warn(string message) => Console.WriteLine($"    [WARN] {message}");
    public static void Error(string message) => Console.Error.WriteLine($"    [ERROR] {message}");
}

internal readonly record struct ProcessResult(int ExitCode, string Output);

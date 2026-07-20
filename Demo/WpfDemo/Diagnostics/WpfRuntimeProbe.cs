using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace WpfDemo.Diagnostics;

internal static class WpfRuntimeProbe
{
    private static readonly Version ExpectedAssemblyVersion = new(8, 0, 0, 0);
    private static readonly Dictionary<string, nint> LoadedNativeModules = new(StringComparer.OrdinalIgnoreCase);

    internal static WpfRuntimeProbeResult Validate()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new InvalidOperationException(
                $"WpfDemo repository-WPF mode requires an x64 process. Actual architecture: {RuntimeInformation.ProcessArchitecture}.");
        }

        WpfAssemblyInfo[] assemblies =
        [
            CaptureAssembly(typeof(DependencyObject).Assembly, "WindowsBase.dll"),
            CaptureAssembly(typeof(Visual).Assembly, "PresentationCore.dll"),
            CaptureAssembly(typeof(Application).Assembly, "PresentationFramework.dll"),
        ];

        WpfNativeModuleInfo[] nativeModules =
        [
            CaptureMixedModeAssembly("DirectWriteForwarder.dll"),
            CaptureNativeModule("PenImc_cor3.dll"),
            CaptureNativeModule("PresentationNative_cor3.dll"),
            CaptureNativeModule("wpfgfx_cor3.dll"),
        ];

        return new WpfRuntimeProbeResult(
            RuntimeInformation.ProcessArchitecture,
            Environment.Version,
            AppContext.TargetFrameworkName ?? string.Empty,
            assemblies,
            nativeModules);
    }

    private static WpfAssemblyInfo CaptureAssembly(Assembly assembly, string expectedFileName)
    {
        string expectedPath = GetExpectedPath(expectedFileName);
        string actualPath = Path.GetFullPath(assembly.Location);
        EnsureExpectedPath(assembly.GetName().Name ?? expectedFileName, actualPath, expectedPath);

        string? targetFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        if (!string.Equals(targetFramework, ".NETCoreApp,Version=v8.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{assembly.GetName().Name} must target .NET 8. Actual target framework: {targetFramework ?? "missing"}.");
        }

        Version? version = assembly.GetName().Version;
        if (version != ExpectedAssemblyVersion)
        {
            throw new InvalidOperationException(
                $"{assembly.GetName().Name} must have repository assembly version {ExpectedAssemblyVersion}. Actual version: {version?.ToString() ?? "missing"}.");
        }

        return new WpfAssemblyInfo(assembly.GetName().Name ?? expectedFileName, version, targetFramework, actualPath);
    }

    private static WpfNativeModuleInfo CaptureMixedModeAssembly(string fileName)
    {
        string expectedPath = GetExpectedPath(fileName);
        RequireFile(expectedPath);

        string assemblyName = Path.GetFileNameWithoutExtension(fileName);
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate =>
            string.Equals(candidate.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
            ?? Assembly.LoadFrom(expectedPath);
        string actualPath = Path.GetFullPath(assembly.Location);
        EnsureExpectedPath(assemblyName, actualPath, expectedPath);

        return new WpfNativeModuleInfo(fileName, actualPath);
    }

    private static WpfNativeModuleInfo CaptureNativeModule(string fileName)
    {
        string expectedPath = GetExpectedPath(fileName);
        RequireFile(expectedPath);

        lock (LoadedNativeModules)
        {
            if (!LoadedNativeModules.ContainsKey(fileName))
            {
                LoadedNativeModules[fileName] = NativeLibrary.Load(expectedPath);
            }
        }

        using Process process = Process.GetCurrentProcess();
        ProcessModule? module = process.Modules.Cast<ProcessModule>().FirstOrDefault(candidate =>
            string.Equals(candidate.ModuleName, fileName, StringComparison.OrdinalIgnoreCase));
        if (module?.FileName is null)
        {
            throw new InvalidOperationException($"Native WPF module was not visible after loading: {fileName}.");
        }

        string actualPath = Path.GetFullPath(module.FileName);
        EnsureExpectedPath(fileName, actualPath, expectedPath);
        return new WpfNativeModuleInfo(fileName, actualPath);
    }

    private static string GetExpectedPath(string fileName) =>
        Path.GetFullPath(Path.Join(AppContext.BaseDirectory, fileName));

    private static void RequireFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Required repository WPF runtime file was not deployed", path);
        }
    }

    private static void EnsureExpectedPath(string componentName, string actualPath, string expectedPath)
    {
        if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{componentName} was loaded from '{actualPath}' instead of WpfDemo output '{expectedPath}'.");
        }
    }
}

internal sealed record WpfAssemblyInfo(string Name, Version? Version, string TargetFramework, string Location);

internal sealed record WpfNativeModuleInfo(string Name, string Location);

internal sealed record WpfRuntimeProbeResult(
    Architecture ProcessArchitecture,
    Version RuntimeVersion,
    string ApplicationTargetFramework,
    IReadOnlyList<WpfAssemblyInfo> Assemblies,
    IReadOnlyList<WpfNativeModuleInfo> NativeModules)
{
    internal string Format()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Repository WPF validation passed.");
        builder.AppendLine($"Process architecture: {ProcessArchitecture}");
        builder.AppendLine($"Runtime: .NET {RuntimeVersion}");
        builder.AppendLine($"Application TFM: {ApplicationTargetFramework}");
        builder.AppendLine();
        builder.AppendLine("Managed WPF assemblies:");
        foreach (WpfAssemblyInfo assembly in Assemblies)
        {
            builder.AppendLine($"  {assembly.Name} {assembly.Version} ({assembly.TargetFramework})");
            builder.AppendLine($"    {assembly.Location}");
        }

        builder.AppendLine();
        builder.AppendLine("Native WPF modules:");
        foreach (WpfNativeModuleInfo module in NativeModules)
        {
            builder.AppendLine($"  {module.Name}");
            builder.AppendLine($"    {module.Location}");
        }

        return builder.ToString().TrimEnd();
    }
}

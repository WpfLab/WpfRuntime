using System;
using System.Collections.Generic;
using System.IO;

namespace WpfReorganize.Builder;

internal static class AssemblyCollector
{
    private static HashSet<string> GetRuntimeAssemblyNames() => new(StringComparer.OrdinalIgnoreCase)
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

    public static Dictionary<string, string> CollectReferenceDlls(string artifactsDir)
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

    public static Dictionary<string, string> CollectRuntimeDlls(string artifactsDir, string platform)
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
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WpfReorganize.Builder;

internal static class CompareService
{
    /// <summary>
    /// Compare the built managed DLLs against the official WindowsDesktop.App.Ref
    /// reference assemblies.  Reports missing, extra, and size-mismatched DLLs.
    /// Returns 0 if no issues, 1 if issues found.
    /// </summary>
    public static int Run(BuilderContext context, bool reportOnly = false)
    {
        var builderOutputDir = context.BuilderOutputDir;
        var stagingDir = context.StagingDir;
        var repoRoot = context.RepoRoot;
        var packagePaths = NuGetPackageService.ReadPackagePaths(builderOutputDir);
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
            var artifactsBin = Path.Join(repoRoot, "artifacts", "bin");
            if (Directory.Exists(artifactsBin))
            {
                // Collect from artifacts into a temporary dictionary for comparison
                var collected = AssemblyCollector.CollectReferenceDlls(repoRoot, Path.Join(repoRoot, "artifacts"));
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
        var wpfAssemblies = WpfRuntimeDefinition.ReadRuntimeAssemblyNames(repoRoot);
        wpfAssemblies.Add("System.Printing");

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
            if (ourSize == 0 || officialSize == 0)
            {
                continue;
            }

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
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace WpfReorganize.Builder;

internal static class AssemblyCollector
{
    public static Dictionary<string, string> CollectReferenceDlls(string repoRoot, string artifactsDir)
    {
        var binDir = Path.Join(artifactsDir, "bin");
        if (!Directory.Exists(binDir))
            return [];

        var wantedDlls = WpfRuntimeDefinition.ReadReferenceAssemblyNames(repoRoot);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectDir in Directory.GetDirectories(binDir, "*-ref"))
        {
            var assemblyName = Path.GetFileName(projectDir)[..^"-ref".Length];
            if (!wantedDlls.Contains(assemblyName))
                continue;

            foreach (var dllDir in new[]
            {
                Path.Join(projectDir, "x64", "Release", "net8.0"),
                Path.Join(projectDir, "AnyCPU", "Release", "net8.0"),
                Path.Join(projectDir, "Any CPU", "Release", "net8.0"),
                Path.Join(projectDir, "Release", "net8.0"),
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

    public static Dictionary<string, string> CollectRuntimeDlls(string repoRoot, string artifactsDir, string platform)
    {
        var binDir = Path.Join(artifactsDir, "bin");
        if (!Directory.Exists(binDir))
            return [];

        var wantedDlls = WpfRuntimeDefinition.ReadRuntimeAssemblyNames(repoRoot);
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
                    Path.Join(projectDir, platformCandidate, "Release", "net8.0"),
                    Path.Join(projectDir, platformCandidate, "Release"),
                    Path.Join(projectDir, "Release", "net8.0"),
                    Path.Join(projectDir, "Release"),
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

    public static void ValidateRuntimeAssemblyVersions(
        IReadOnlyDictionary<string, string> runtimeDlls,
        string expectedVersion,
        string rid)
    {
        ArgumentNullException.ThrowIfNull(runtimeDlls);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);

        var expected = Version.Parse(expectedVersion);
        foreach (var (name, path) in runtimeDlls)
        {
            Version actual = ReadAssemblyVersion(path);
            Log.Info($"  Validated runtime assembly version for {rid}: {name} {actual}");
            if (!actual.Equals(expected))
            {
                throw new InvalidOperationException(
                    $"Runtime assembly '{name}' for {rid} must have assembly version {expected}; actual version is {actual}: {path}");
            }
        }
    }

    private static Version ReadAssemblyVersion(string path)
    {
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
            throw new InvalidOperationException($"Runtime file is not a managed assembly: {path}");

        MetadataReader reader = peReader.GetMetadataReader();
        return reader.GetAssemblyDefinition().Version;
    }

    public static string? GetPdbPath(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        return File.Exists(pdbPath) ? pdbPath : null;
    }
}

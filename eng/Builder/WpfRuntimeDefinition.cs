using System.Xml.Linq;

namespace WpfReorganize.Builder;

internal static class WpfRuntimeDefinition
{
    public static HashSet<string> ReadReferenceAssemblyNames(string repoRoot) =>
        ReadAssemblyNames(repoRoot, includeReferenceOnly: true);

    public static HashSet<string> ReadRuntimeAssemblyNames(string repoRoot) =>
        ReadAssemblyNames(repoRoot, includeReferenceOnly: false);

    public static IReadOnlyList<PackageDependency> ReadRuntimePackageDependencies(string repoRoot)
    {
        var runtimePropsPath = GetRuntimePropsPath(repoRoot);
        var versionsPropsPath = Path.Join(repoRoot, "eng", "Versions.props");
        var runtimeDocument = XDocument.Load(runtimePropsPath);
        var versionsDocument = XDocument.Load(versionsPropsPath);
        var dependencies = new List<PackageDependency>();

        foreach (var element in runtimeDocument.Descendants().Where(element => element.Name.LocalName == "RepoWpfRuntimePackage"))
        {
            var id = GetRequiredAttribute(element, runtimePropsPath, "Include");
            var versionProperty = GetRequiredAttribute(element, runtimePropsPath, "VersionProperty");
            var version = ReadMsBuildProperty(versionsDocument, versionsPropsPath, versionProperty);
            dependencies.Add(new PackageDependency(id, version));
        }

        if (dependencies.Count == 0)
            throw new InvalidOperationException($"No RepoWpfRuntimePackage items were found in {runtimePropsPath}");

        return dependencies;
    }

    private static HashSet<string> ReadAssemblyNames(string repoRoot, bool includeReferenceOnly)
    {
        var runtimePropsPath = GetRuntimePropsPath(repoRoot);
        var document = XDocument.Load(runtimePropsPath);
        var names = document
            .Descendants()
            .Where(element => element.Name.LocalName == "RepoWpfRuntimeAssembly")
            .Where(element =>
                !includeReferenceOnly ||
                !string.Equals((string?)element.Attribute("PackReference"), "false", StringComparison.OrdinalIgnoreCase))
            .Select(element => GetRequiredAttribute(element, runtimePropsPath, "Include"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (names.Count == 0)
            throw new InvalidOperationException($"No RepoWpfRuntimeAssembly items were found in {runtimePropsPath}");

        return names;
    }

    private static string GetRuntimePropsPath(string repoRoot)
    {
        var path = Path.Join(repoRoot, "eng", "WpfRuntimeDependencies.props");
        if (!File.Exists(path))
            throw new FileNotFoundException("Shared WPF runtime definition was not found", path);

        return path;
    }

    private static string GetRequiredAttribute(XElement element, string path, string attributeName)
    {
        var value = (string?)element.Attribute(attributeName);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{element.Name.LocalName} is missing required attribute '{attributeName}' in {path}");

        return value;
    }

    private static string ReadMsBuildProperty(XDocument document, string propsPath, string propertyName)
    {
        var values = document
            .Descendants()
            .Where(element =>
                element.Parent?.Name.LocalName == "PropertyGroup" &&
                element.Name.LocalName == propertyName)
            .Select(element => element.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (values.Count == 0)
            throw new InvalidOperationException($"MSBuild property '{propertyName}' was not found in {propsPath}");
        if (values.Count > 1)
            throw new InvalidOperationException($"MSBuild property '{propertyName}' is defined multiple times in {propsPath}");

        return values[0];
    }
}
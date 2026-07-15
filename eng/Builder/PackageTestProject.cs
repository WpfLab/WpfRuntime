namespace WpfReorganize.Builder;

internal sealed record PackageTestProject(string Name, string ProjectPath, IReadOnlyList<string> TargetFrameworks);

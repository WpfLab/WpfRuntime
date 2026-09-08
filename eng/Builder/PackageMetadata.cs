namespace WpfReorganize.Builder;

internal static class PackageMetadata
{
    public const string Id = "WpfLab.WpfRuntime";
    public const string ProjectUrl = "https://github.com/WpfLab/WpfRuntime";
    public const string RuntimeAssemblyVersion = "42.42.42.42424";

    public static IReadOnlyList<string> TargetFrameworks { get; } = ["net8.0", "net9.0"];
}

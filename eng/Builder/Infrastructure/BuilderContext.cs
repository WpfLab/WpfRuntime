namespace WpfReorganize.Builder;

internal sealed class BuilderContext
{
    private BuilderContext(string repoRoot)
    {
        RepoRoot = repoRoot;
        ArtifactsDir = Path.Join(repoRoot, "artifacts");
        BuildLogsDir = Path.Join(ArtifactsDir, "log", "Builder");
        BuilderOutputDir = Path.Join(repoRoot, "eng", "Builder", "bin");
        StagingDir = Path.Join(BuilderOutputDir, "staging");
        NupkgOutputDir = Path.Join(BuilderOutputDir, "nupkg");
        SourceDir = Path.Join(repoRoot, "src", "Microsoft.DotNet.Wpf", "src");
    }

    public string RepoRoot { get; }

    public string ArtifactsDir { get; }

    public string BuildLogsDir { get; }

    public string BuilderOutputDir { get; }

    public string StagingDir { get; }

    public string NupkgOutputDir { get; }

    public string SourceDir { get; }

    public static BuilderContext Create() => new(RepositoryLocator.FindRepoRoot());
}

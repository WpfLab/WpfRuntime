namespace WpfReorganize.Builder;

internal sealed record ProcessRunOptions
{
    public ProcessRunOptions(string fileName, string workingDirectory, params string[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

        FileName = fileName;
        WorkingDirectory = workingDirectory;
        Arguments = [.. arguments];
    }

    public string FileName { get; }

    public string WorkingDirectory { get; }

    public IReadOnlyList<string> Arguments { get; }

    public TimeSpan? Timeout { get; init; }

    public bool InheritEnvironment { get; init; } = true;

    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } =
        new Dictionary<string, string?>();

    public string? LogPath { get; init; }
}

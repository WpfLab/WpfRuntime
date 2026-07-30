namespace WpfReorganize.Builder;

internal sealed class PullRequestRelayWorkspace
{
    private const string StateFileName = "state.json";
    private static readonly string TemporaryRoot = Path.GetFullPath
    (
        Path.Join(Path.GetTempPath(), "WpfRuntimeTemp")
    );

    private PullRequestRelayWorkspace(string rootPath)
    {
        RootPath = rootPath;
        RepositoryPath = Path.Join(rootPath, "repository");
        LogsPath = Path.Join(rootPath, "logs");
        IsolatedHomePath = Path.Join(rootPath, "home");
        StatePath = Path.Join(rootPath, StateFileName);
    }

    public string RootPath { get; }

    public string RepositoryPath { get; }

    public string LogsPath { get; }

    public string IsolatedHomePath { get; }

    public string StatePath { get; }

    public static PullRequestRelayWorkspace Create(PullRequestAddress source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Directory.CreateDirectory(TemporaryRoot);
        var repositoryName = SanitizePathSegment(source.Repository);
        var name = $"{repositoryName}-{source.Number}-{DateTime.UtcNow:MMddHHmmss}";
        var workspace = new PullRequestRelayWorkspace(Path.Join(TemporaryRoot, name));
        Directory.CreateDirectory(workspace.RootPath);
        Directory.CreateDirectory(workspace.LogsPath);
        Directory.CreateDirectory(workspace.IsolatedHomePath);
        return workspace;
    }

    public static PullRequestRelayWorkspace Open(string rootPath) =>
        new(Path.GetFullPath(rootPath));

    public async Task<PullRequestRelayState> ReadStateAsync(CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            StatePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        return await System.Text.Json.JsonSerializer.DeserializeAsync<PullRequestRelayState>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Unable to read relay state from '{StatePath}'.");
    }

    public async Task WriteStateAsync(PullRequestRelayState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        await using var stream = new FileStream(
            StatePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        await System.Text.Json.JsonSerializer.SerializeAsync(
                stream,
                state,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public void Delete()
    {
        var fullRootPath = Path.GetFullPath(RootPath);
        var relativePath = Path.GetRelativePath(TemporaryRoot, fullRootPath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath)
            || string.Equals(relativePath, ".", StringComparison.Ordinal)
            || !File.Exists(StatePath))
        {
            throw new InvalidOperationException($"Refusing to delete an unrecognized relay workspace: {RootPath}");
        }

        ClearReadOnlyAttributes(fullRootPath);
        Directory.Delete(fullRootPath, recursive: true);
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
    }

    private static void ClearReadOnlyAttributes(string rootPath)
    {
        var directories = new Stack<string>();
        directories.Push(rootPath);
        while (directories.Count > 0)
        {
            var directory = directories.Pop();
            foreach (var filePath in Directory.EnumerateFiles(directory))
            {
                var attributes = File.GetAttributes(filePath);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
                }
            }

            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                var attributes = File.GetAttributes(childDirectory);
                if ((attributes & FileAttributes.ReparsePoint) == 0)
                {
                    directories.Push(childDirectory);
                }
            }
        }
    }
}

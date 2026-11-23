namespace WpfReorganize.Builder;

internal sealed class PullRequestRelayWorkspace
{
    private const string StateFileName = "state.json";
    private const string OwnershipFileName = ".relay-workspace";
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

    /// <summary>Creates a dedicated relay directory under the requested parent or the default temporary root.</summary>
    public static PullRequestRelayWorkspace Create(PullRequestAddress source, string? workspaceRoot = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var parentPath = string.IsNullOrWhiteSpace(workspaceRoot) ? TemporaryRoot : Path.GetFullPath(workspaceRoot);
        var repositoryName = SanitizePathSegment(source.Repository);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var name = $"{repositoryName}-{source.Number}-{DateTime.UtcNow:MMddHHmmss}-{suffix}";
        var workspace = new PullRequestRelayWorkspace(Path.Join(parentPath, name));
        Directory.CreateDirectory(workspace.RootPath);
        // Bind cleanup to the original child path, even when resuming in another process.
        using (var marker = new StreamWriter(new FileStream(Path.Join(workspace.RootPath, OwnershipFileName), FileMode.CreateNew)))
        {
            marker.Write(workspace.RootPath);
        }

        Directory.CreateDirectory(workspace.LogsPath);
        Directory.CreateDirectory(workspace.IsolatedHomePath);
        return workspace;
    }

    public static PullRequestRelayWorkspace Open(string rootPath) =>
        new(Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath)));

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
        if (!File.Exists(StatePath)
            || (File.GetAttributes(RootPath) & FileAttributes.ReparsePoint) != 0
            || !IsOwnedWorkspace())
        {
            throw new InvalidOperationException($"Refusing to delete an unrecognized relay workspace: {RootPath}");
        }

        ClearReadOnlyAttributes(RootPath);
        Directory.Delete(RootPath, recursive: true);
    }

    private bool IsOwnedWorkspace()
    {
        var markerPath = Path.Join(RootPath, OwnershipFileName);
        if (File.Exists(markerPath))
        {
            if ((File.GetAttributes(markerPath) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            var originalPath = File.ReadAllText(markerPath);
            return Path.IsPathFullyQualified(originalPath) && Path.GetRelativePath(RootPath, originalPath) == ".";
        }

        // Older runs did not write a marker and could only create direct children of TemporaryRoot.
        var relativePath = Path.GetRelativePath(TemporaryRoot, RootPath);
        return System.Text.RegularExpressions.Regex.IsMatch(relativePath, @"\A[A-Za-z0-9_.-]+-[0-9]+-[0-9]{10}\z");
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
                if ((attributes & (FileAttributes.ReadOnly | FileAttributes.ReparsePoint)) == FileAttributes.ReadOnly)
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

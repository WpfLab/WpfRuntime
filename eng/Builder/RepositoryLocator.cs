namespace WpfReorganize.Builder;

internal static class RepositoryLocator
{
    public static string FindRepoRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (Directory.Exists(Path.Join(directory, ".git")))
            {
                return directory;
            }

            var parent = Path.GetDirectoryName(directory);
            if (parent == directory)
            {
                break;
            }

            directory = parent;
        }

        throw new InvalidOperationException("Unable to find repository root (.git directory)");
    }
}

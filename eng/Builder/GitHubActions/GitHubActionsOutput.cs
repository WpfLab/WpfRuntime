namespace WpfReorganize.Builder;

internal static class GitHubActionsOutput
{
    public static void Write(string outputPath, IReadOnlyDictionary<string, string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(values);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = File.AppendText(outputPath);
        foreach (var (name, value) in values)
        {
            ValidateName(name);
            if (value.Contains('\r') || value.Contains('\n'))
            {
                throw new ArgumentException(BuilderResources.GitHubActionsOutputMustBeSingleLine, nameof(values));
            }

            writer.Write(name);
            writer.Write('=');
            writer.WriteLine(value);
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException(BuilderResources.InvalidGitHubActionsOutputName, nameof(name));
        }
    }
}

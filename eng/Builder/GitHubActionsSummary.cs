namespace WpfReorganize.Builder;

using System.Text;

internal static class GitHubActionsSummary
{
    public static void Write(string? summaryPath, string heading, string message)
    {
        if (string.IsNullOrWhiteSpace(summaryPath))
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var directory = Path.GetDirectoryName(summaryPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(
            summaryPath,
            $"## {Sanitize(heading, 120)}{Environment.NewLine}{Environment.NewLine}" +
            $"{Sanitize(message, 500)}{Environment.NewLine}",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string Sanitize(string value, int maximumLength)
    {
        var sanitized = new string(value
            .Where(character => !char.IsControl(character) || character is '\r' or '\n' or '\t')
            .Take(maximumLength)
            .ToArray());
        return sanitized.Replace("@", "@\u200b", StringComparison.Ordinal);
    }
}

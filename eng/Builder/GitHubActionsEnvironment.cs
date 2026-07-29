namespace WpfReorganize.Builder;

internal static class GitHubActionsEnvironment
{
    public static string GetRequired(string? optionValue, string environmentName)
    {
        var value = string.IsNullOrWhiteSpace(optionValue)
            ? Environment.GetEnvironmentVariable(environmentName)
            : optionValue;
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(string.Format(
                BuilderResources.GitHubActionsEnvironmentVariableRequired,
                environmentName))
            : value;
    }

    public static long GetPositiveInt64(long? optionValue, string environmentName)
    {
        if (optionValue is > 0)
        {
            return optionValue.Value;
        }

        var environmentValue = Environment.GetEnvironmentVariable(environmentName);
        if (long.TryParse(environmentValue, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        throw new ArgumentException(string.Format(
            BuilderResources.GitHubActionsEnvironmentVariableRequired,
            environmentName));
    }
}

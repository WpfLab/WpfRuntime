using System.Collections;

namespace WpfReorganize.Builder;

internal static class ProcessEnvironment
{
    private static readonly string[] SensitiveNameFragments =
    [
        "TOKEN",
        "SECRET",
        "PASSWORD",
        "CREDENTIAL",
        "PRIVATE_KEY",
        "API_KEY",
        "ACCESS_KEY",
        "CONNECTION_STRING",
    ];

    private static readonly string[] SensitiveNamePrefixes =
    [
        "ACTIONS_",
        "ARM_",
        "ARTIFACTS_CREDENTIALPROVIDER_",
        "AWS_",
        "AZURE_",
        "GCLOUD_",
        "GH_",
        "GITHUB_",
        "GOOGLE_",
        "NUGETPACKAGESOURCECREDENTIALS_",
        "RUNNER_",
        "VSS_NUGET_",
    ];

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "GIT_ASKPASS",
        "GIT_PROXY_COMMAND",
        "GIT_SSH",
        "GIT_SSH_COMMAND",
        "NUGET_API_KEY",
        "NUGET_AUTH_TOKEN",
        "NUGET_CREDENTIALPROVIDERS_PATH",
        "SSH_AGENT_PID",
        "SSH_ASKPASS",
        "SSH_AUTH_SOCK",
        "SYSTEM_ACCESSTOKEN",
    };

    public static IReadOnlyDictionary<string, string?> CreateUntrustedBuildEnvironment(string isolatedHome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isolatedHome);
        Directory.CreateDirectory(isolatedHome);

        var gitConfigPath = Path.Join(isolatedHome, ".gitconfig");
        if (!File.Exists(gitConfigPath))
        {
            File.WriteAllText(gitConfigPath, string.Empty);
        }

        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            var name = (string)variable.Key;
            if (!IsSensitive(name) && !name.StartsWith("GIT_CONFIG_", StringComparison.OrdinalIgnoreCase))
            {
                environment[name] = variable.Value?.ToString();
            }
        }

        environment["HOME"] = isolatedHome;
        environment["USERPROFILE"] = isolatedHome;
        environment["DOTNET_CLI_HOME"] = Path.Join(isolatedHome, ".dotnet");
        environment["APPDATA"] = Path.Join(isolatedHome, "AppData", "Roaming");
        environment["LOCALAPPDATA"] = Path.Join(isolatedHome, "AppData", "Local");
        environment["NUGET_PACKAGES"] = Path.Join(isolatedHome, ".nuget", "packages");
        environment["TEMP"] = Path.Join(isolatedHome, "Temp");
        environment["TMP"] = Path.Join(isolatedHome, "Temp");
        environment["GIT_CONFIG_GLOBAL"] = gitConfigPath;
        environment["GIT_CONFIG_NOSYSTEM"] = "1";
        environment["GIT_TERMINAL_PROMPT"] = "0";
        environment["GCM_INTERACTIVE"] = "Never";
        environment["DOTNET_NOLOGO"] = "1";
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        foreach (var directory in new[]
        {
            environment["DOTNET_CLI_HOME"],
            environment["APPDATA"],
            environment["LOCALAPPDATA"],
            environment["NUGET_PACKAGES"],
            environment["TEMP"],
        })
        {
            Directory.CreateDirectory(directory!);
        }

        return environment;
    }

    public static IReadOnlyDictionary<string, string?> CreateGitEnvironment(string isolatedHome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isolatedHome);
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["GITHUB_TOKEN"] = null,
            ["GH_TOKEN"] = null,
            ["ACTIONS_ID_TOKEN_REQUEST_TOKEN"] = null,
            ["ACTIONS_ID_TOKEN_REQUEST_URL"] = null,
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GCM_INTERACTIVE"] = "Never",
            ["GIT_OPTIONAL_LOCKS"] = "0",
        };
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            var name = (string)variable.Key;
            if (name.StartsWith("GIT_CONFIG_", StringComparison.OrdinalIgnoreCase))
            {
                environment[name] = null;
            }
        }
        return environment;
    }

    internal static bool IsSensitive(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return SensitiveNames.Contains(name)
            || SensitiveNamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || SensitiveNameFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}

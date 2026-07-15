namespace WpfReorganize.Builder;

internal static class MsBuildService
{
    public static string FindMsBuild()
    {
        var vswhere = Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");

        if (File.Exists(vswhere))
        {
            foreach (var pattern in new[]
            {
                "MSBuild\\**\\Bin\\amd64\\MSBuild.exe",
                "MSBuild\\**\\Bin\\MSBuild.exe",
            })
            {
                var result = ProcessRunner.Run(vswhere,
                    $"-latest -requires Microsoft.Component.MSBuild -find {pattern}",
                    AppContext.BaseDirectory);
                if (result.ExitCode == 0)
                {
                    var path = result.Output.Trim();
                    path = path.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        Log.Info($"  MSBuild found via vswhere: {path}");
                        return path;
                    }
                }
            }
        }

        var pathResult = ProcessRunner.Run("where", "msbuild", AppContext.BaseDirectory);
        if (pathResult.ExitCode == 0)
        {
            var path = pathResult.Output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return path;
        }

        var candidates = new List<string>();
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (var edition in new[] { "Enterprise", "Professional", "Community", "BuildTools" })
        {
            candidates.Add(Path.Join(programFiles, "Microsoft Visual Studio", "2022", edition, "MSBuild", "Current", "Bin", "MSBuild.exe"));
            candidates.Add(Path.Join(programFiles, "Microsoft Visual Studio", "2026", edition, "MSBuild", "Current", "Bin", "MSBuild.exe"));
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        Log.Warn("Could not locate MSBuild.exe via vswhere or well-known paths; falling back to 'msbuild' on PATH");
        return "msbuild";
    }

    public static string GetBuildLogPath(string buildLogsDir, string projectName, string platform)
    {
        var invalidFileNameChars = Path.GetInvalidFileNameChars();
        var safeProjectName = new string(projectName.Select(character => invalidFileNameChars.Contains(character) ? '_' : character).ToArray());
        return Path.Join(buildLogsDir, $"{safeProjectName}-{platform}.log");
    }

    public static string GetFileLoggerArguments(string logPath) =>
        $" /fl /flp:\"logfile={logPath};verbosity=diagnostic;encoding=UTF-8\"";

    public static void LogBuildFailure(
        string projectName,
        string platform,
        string msbuildExe,
        string arguments,
        string workingDirectory,
        string logPath,
        ProcessResult result)
    {
        Log.Error($"Build failed: {projectName} ({platform})");
        Log.Error($"Exit code: {result.ExitCode}; elapsed: {result.Elapsed.TotalSeconds:F1}s");
        Log.Error($"Working directory: {workingDirectory}");
        Log.Error($"Command: \"{msbuildExe}\" {arguments}");

        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            Log.Error("MSBuild console output:");
            WriteIndentedErrorLines(result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }
        else
        {
            Log.Error("MSBuild produced no console error output. Reading the diagnostic file log instead.");
        }

        if (!File.Exists(logPath))
        {
            Log.Error($"Diagnostic MSBuild log was not created: {logPath}");
            return;
        }

        Log.Error($"Diagnostic MSBuild log: {logPath}");
        var errorLines = new Queue<string>();
        var logTail = new Queue<string>();
        var uniqueErrorLines = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(logPath))
        {
            EnqueueWithLimit(logTail, line, 100);
            if (IsDiagnosticErrorLine(line) && uniqueErrorLines.Add(line))
            {
                EnqueueWithLimit(errorLines, line, 100);
            }
        }

        if (errorLines.Count > 0)
        {
            Log.Error("Error lines from diagnostic log:");
            WriteIndentedErrorLines(errorLines);
        }
        else
        {
            Log.Error("No explicit error line was found in the diagnostic log.");
        }

        Log.Error("Last 100 lines from diagnostic log:");
        WriteIndentedErrorLines(logTail);
    }

    private static bool IsDiagnosticErrorLine(string line) =>
        line.Contains(" error ", StringComparison.OrdinalIgnoreCase)
        || line.Contains(": error", StringComparison.OrdinalIgnoreCase)
        || line.Contains("exception", StringComparison.OrdinalIgnoreCase)
        || (line.Contains("MSB", StringComparison.OrdinalIgnoreCase)
            && line.Contains("error", StringComparison.OrdinalIgnoreCase));

    private static void EnqueueWithLimit(Queue<string> lines, string line, int limit)
    {
        lines.Enqueue(line);
        if (lines.Count > limit)
        {
            lines.Dequeue();
        }
    }

    private static void WriteIndentedErrorLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            Console.Error.WriteLine($"      {line}");
        }
    }
}

using System.Diagnostics;

namespace WpfReorganize.Builder;

internal static class ProcessRunner
{
    public static ProcessResult Run(string fileName, string arguments, string workingDirectory, TimeSpan? timeout = null)
    {
        var startTime = Stopwatch.GetTimestamp();
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var exited = timeout is null
            ? process.WaitForExit(int.MaxValue)
            : process.WaitForExit((int)timeout.Value.TotalMilliseconds);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Task.WaitAll(outputTask, errorTask);
            throw new TimeoutException($"Process timed out after {timeout!.Value.TotalSeconds:F0} seconds: {fileName} {arguments}");
        }

        Task.WaitAll(outputTask, errorTask);

        return new ProcessResult(process.ExitCode, outputTask.Result + errorTask.Result, Stopwatch.GetElapsedTime(startTime));
    }
}

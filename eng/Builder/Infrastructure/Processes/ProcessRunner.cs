using System.Diagnostics;
using System.Text;
using System.Threading;

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

        return new ProcessResult(
            process.ExitCode,
            outputTask.Result,
            errorTask.Result,
            Stopwatch.GetElapsedTime(startTime));
    }

    public static ProcessResult Run(ProcessRunOptions options, CancellationToken cancellationToken = default) =>
        RunAsync(options, cancellationToken).GetAwaiter().GetResult();

    public static async Task<ProcessResult> RunAsync(
        ProcessRunOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var startTime = Stopwatch.GetTimestamp();
        var startInfo = CreateStartInfo(options);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {options.FileName}");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeoutCancellationTokenSource = options.Timeout is null
            ? null
            : new CancellationTokenSource(options.Timeout.Value);
        using var linkedCancellationTokenSource = timeoutCancellationTokenSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellationTokenSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linkedCancellationTokenSource.IsCancellationRequested)
        {
            KillProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            var canceledOutput = await ReadOutputAsync(outputTask, errorTask).ConfigureAwait(false);
            await WriteLogAsync(options.LogPath, canceledOutput.StandardOutput, canceledOutput.StandardError)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    $"Process was canceled: {FormatCommand(options)}",
                    innerException: null,
                    cancellationToken);
            }

            throw new TimeoutException(
                $"Process timed out after {options.Timeout!.Value.TotalSeconds:F0} seconds: {FormatCommand(options)}");
        }

        var output = await ReadOutputAsync(outputTask, errorTask).ConfigureAwait(false);
        await WriteLogAsync(options.LogPath, output.StandardOutput, output.StandardError).ConfigureAwait(false);
        return new ProcessResult(
            process.ExitCode,
            output.StandardOutput,
            output.StandardError,
            Stopwatch.GetElapsedTime(startTime));
    }

    private static ProcessStartInfo CreateStartInfo(ProcessRunOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.FileName,
            WorkingDirectory = options.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in options.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!options.InheritEnvironment)
        {
            startInfo.Environment.Clear();
        }

        foreach (var (name, value) in options.EnvironmentVariables)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
        }

        return startInfo;
    }

    private static void KillProcessTree(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
        }
    }

    private static async Task<(string StandardOutput, string StandardError)> ReadOutputAsync(
        Task<string> outputTask,
        Task<string> errorTask)
    {
        await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
        return (outputTask.Result, errorTask.Result);
    }

    private static async Task WriteLogAsync(string? logPath, string standardOutput, string standardError)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
                logPath,
                standardOutput + standardError,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            .ConfigureAwait(false);
    }

    private static string FormatCommand(ProcessRunOptions options) =>
        string.Join(' ', new[] { options.FileName }.Concat(options.Arguments.Select(QuoteArgument)));

    private static string QuoteArgument(string argument) =>
        argument.Any(char.IsWhiteSpace) ? $"\"{argument.Replace("\"", "\\\"")}\"" : argument;
}

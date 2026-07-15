namespace WpfReorganize.Builder;

internal readonly record struct ProcessResult(int ExitCode, string Output, TimeSpan Elapsed);

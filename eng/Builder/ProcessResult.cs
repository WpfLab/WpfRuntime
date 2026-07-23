namespace WpfReorganize.Builder;

internal readonly record struct ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Elapsed)
{
    public string Output => StandardOutput + StandardError;
}

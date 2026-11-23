namespace WpfReorganize.Builder;

internal sealed class SourceHeadUnavailableException : InvalidOperationException
{
    public SourceHeadUnavailableException(string message)
        : base(message)
    {
    }
}
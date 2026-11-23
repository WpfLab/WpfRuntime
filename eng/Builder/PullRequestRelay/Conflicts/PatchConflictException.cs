namespace WpfReorganize.Builder;

internal sealed class PatchConflictException : InvalidOperationException
{
    public PatchConflictException(string patchPath, string gitOutput)
        : base(gitOutput)
    {
        PatchPath = patchPath;
    }

    public string PatchPath { get; }
}
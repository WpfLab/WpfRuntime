namespace WpfReorganize.Builder;

internal sealed class NoChangesToRelayException : InvalidOperationException
{
    public NoChangesToRelayException(GitObjectId relayCommit)
        : base(BuilderResources.NoChangesToRelay)
    {
        RelayCommit = relayCommit;
    }

    public GitObjectId RelayCommit { get; }
}
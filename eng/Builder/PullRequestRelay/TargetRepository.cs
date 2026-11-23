namespace WpfReorganize.Builder;

internal sealed record TargetRepository(
    string RemoteName,
    GitHubRepositoryAddress Address,
    string FetchUrl,
    string PushUrl,
    string BaseBranch,
    string RelayBranch,
    GitObjectId BaseSha,
    GitObjectId? ExistingRelayBranchSha);
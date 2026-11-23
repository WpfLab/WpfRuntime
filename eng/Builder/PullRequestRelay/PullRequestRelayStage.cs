namespace WpfReorganize.Builder;

internal enum PullRequestRelayStage
{
    InputValidated,
    TargetResolved,
    SourceResolved,
    RepositoryCloned,
    SourceFetched,
    ConflictResolutionRequired,
    ChangesApplied,
    LocalValidationSkipped,
    LocalValidationSucceeded,
    BranchPushed,
    PullRequestCreatedOrReused,
}
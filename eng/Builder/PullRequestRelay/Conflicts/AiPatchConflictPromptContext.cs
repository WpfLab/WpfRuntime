namespace WpfReorganize.Builder;

internal readonly record struct AiPatchConflictPromptContext
(
    string OutputDirectoryPath,
    string SourcePullRequestUrl,
    string SourceBaseSha,
    string SourceHeadSha,
    string TargetRepository,
    string TargetBaseBranch,
    string RepositoryPath,
    string PatchPath,
    string WorkspacePath,
    string BuilderProjectPath
);
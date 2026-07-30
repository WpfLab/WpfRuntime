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

internal static class AiPatchConflictPromptWriter
{
    internal const string ChineseFileName = "AI_PATCH_CONFLICT_PROMPT.zh-CN.md";
    internal const string EnglishFileName = "AI_PATCH_CONFLICT_PROMPT.en-US.md";

    internal static async Task WriteAsync
    (
        AiPatchConflictPromptContext context,
        CancellationToken cancellationToken
    )
    {
        Directory.CreateDirectory(context.OutputDirectoryPath);
        await File.WriteAllTextAsync
        (
            Path.Join(context.OutputDirectoryPath, ChineseFileName),
            CreateChinesePrompt(context),
            cancellationToken
        ).ConfigureAwait(false);
        await File.WriteAllTextAsync
        (
            Path.Join(context.OutputDirectoryPath, EnglishFileName),
            CreateEnglishPrompt(context),
            cancellationToken
        ).ConfigureAwait(false);
    }

    internal static string CreateChinesePrompt
    (
        AiPatchConflictPromptContext context
    ) =>
        $"""
        # AI 任务：协助解决 PR Relay Patch 冲突

        ## 背景

        Builder 正在把来源 Pull Request 的文件变化搬运到目标仓库。此流程只搬运文件变化，不搬运来源提交历史，也不执行 Git merge 或 cherry-pick。

        - 来源 PR：{context.SourcePullRequestUrl}
        - 来源差异范围：{context.SourceBaseSha} 到 {context.SourceHeadSha}
        - 目标仓库：{context.TargetRepository}
        - 目标基线：{context.TargetBaseBranch}
        - Relay workspace：{context.WorkspacePath}
        - 待操作仓库：{context.RepositoryPath}
        - 待合并 Patch：{context.PatchPath}

        自动应用 Patch 时发生冲突，需要你协助开发者理解来源修改的意图，并把这些文件变化合理地合入当前目标基线。

        ## 必须遵守

        1. 只在 `{context.RepositoryPath}` 中操作。
        2. 使用 `{context.PatchPath}` 作为需要合入的 Patch。
        3. 不要执行 `git commit`。解决完成后只执行 `git add` 暂存已解决的文件。
        4. 不要执行 `git merge`、`git cherry-pick`、`git rebase`、`git push` 或改写历史。
        5. 不要机械地选择冲突某一侧。应同时理解目标基线的当前实现与 Patch 的修改意图，保留两者中仍然有效的逻辑。
        6. 不要顺手修改与 Patch 无关的代码，不要做额外重构或格式化。

        ## 常规处理步骤

        1. 进入待操作仓库：

           ```cmd
           cd /d "{context.RepositoryPath}"
           ```

        2. 应用 Patch，并让 Git 尝试三方合并：

           ```cmd
           git apply --index --3way --ignore-space-change --ignore-whitespace "{context.PatchPath}"
           ```

           命令返回非零并显示冲突是当前预期情况，不代表应该放弃处理。

        3. 执行 `git status` 找出冲突文件。阅读 Patch、冲突标记、相关调用方和邻近代码，判断来源改动要解决的问题。对于重命名、移动、签名变化或目标分支已有等价实现的情况，应把来源意图适配到目标代码，而不是逐行照搬。

        4. 编辑所有冲突文件，移除 `<<<<<<<`、`=======`、`>>>>>>>` 冲突标记。注意保持现有代码风格、换行方式、可空性约定和项目结构；二进制文件不要手工编辑。

        5. 执行 `git diff`、`git diff --check` 和 `git status` 检查结果。

        6. 使用 `git add <已解决的文件路径>` 暂存已解决文件。

        7. 再次执行 `git status`，确认没有未解决冲突。不要执行 `git commit`，Builder 会创建统一的 Relay commit。

        8. 由开发者执行以下命令恢复流水线：

           ```cmd
           dotnet run --project "{context.BuilderProjectPath}" -- relay-pr --resume-workspace "{context.WorkspacePath}"
           ```

           只有开发者希望在发布前额外执行本地构建验证时，才在命令末尾添加 `--allow-untrusted-build`。

        ## 完成标准

        - Patch 表达的有效文件变化已经适配到目标基线。
        - 所有冲突标记均已移除。
        - 已解决文件已经执行 `git add`。
        - 没有执行 commit、push 或任何合并历史的操作。
        - 最终回复开发者时，简要说明解决了哪些冲突、采用了什么取舍，并提醒其运行上面的恢复命令。
        """;

    internal static string CreateEnglishPrompt
    (
        AiPatchConflictPromptContext context
    ) =>
        $"""
        # AI Task: Resolve a PR Relay Patch Conflict

        ## Background

        Builder is relaying file changes from a source pull request onto a target repository. This process transfers file changes only. It must not transfer the source commit history or use Git merge or cherry-pick.

        - Source PR: {context.SourcePullRequestUrl}
        - Source diff range: {context.SourceBaseSha} to {context.SourceHeadSha}
        - Target repository: {context.TargetRepository}
        - Target base: {context.TargetBaseBranch}
        - Relay workspace: {context.WorkspacePath}
        - Repository to edit: {context.RepositoryPath}
        - Patch to apply: {context.PatchPath}

        Automatic Patch application produced conflicts. Help the developer understand the intent of the source changes and adapt those changes correctly to the current target base.

        ## Required Constraints

        1. Work only inside `{context.RepositoryPath}`.
        2. Use `{context.PatchPath}` as the Patch to apply.
        3. Do not run `git commit`. After resolving conflicts, only run `git add` for resolved files.
        4. Do not run `git merge`, `git cherry-pick`, `git rebase`, `git push`, or rewrite history.
        5. Do not mechanically choose one conflict side. Understand both the current target implementation and the intent of the Patch, preserving all logic that remains valid.
        6. Do not change unrelated code or perform extra refactoring or formatting.

        ## Normal Procedure

        1. Enter the repository:

           ```cmd
           cd /d "{context.RepositoryPath}"
           ```

        2. Apply the Patch and let Git attempt a three-way application:

           ```cmd
           git apply --index --3way --ignore-space-change --ignore-whitespace "{context.PatchPath}"
           ```

           A non-zero exit code with conflicts is expected here and does not mean the task should be abandoned.

        3. Run `git status` to locate conflicted files. Read the Patch, conflict markers, callers, and nearby code to understand the source change. For renames, moves, signature changes, or equivalent target-side implementations, adapt the intent instead of copying lines mechanically.

        4. Edit every conflicted file and remove all `<<<<<<<`, `=======`, and `>>>>>>>` markers. Preserve the existing code style, line endings, nullable conventions, and project structure. Do not manually edit binary files.

        5. Review the result with `git diff`, `git diff --check`, and `git status`.

        6. Stage resolved files with `git add <resolved-file-path>`.

        7. Run `git status` again and ensure no unresolved conflicts remain. Do not run `git commit`; Builder will create the standard relay commit.

        8. Ask the developer to resume the pipeline with:

           ```cmd
           dotnet run --project "{context.BuilderProjectPath}" -- relay-pr --resume-workspace "{context.WorkspacePath}"
           ```

           Add `--allow-untrusted-build` only when the developer also wants to run the optional local build validation before publishing.

        ## Completion Criteria

        - Valid changes represented by the Patch are adapted to the target base.
        - All conflict markers are removed.
        - Resolved files are staged with `git add`.
        - No commit, push, or history-merging operation was performed.
        - In the final response, briefly describe the conflicts and decisions, then remind the developer to run the resume command above.
        """;
}

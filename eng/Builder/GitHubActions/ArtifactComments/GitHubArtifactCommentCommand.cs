using DotNetCampus.Cli;
using DotNetCampus.Cli.Compiler;

namespace WpfReorganize.Builder;

[Command("comment-pr-artifacts")]
internal sealed class GitHubArtifactCommentCommand : ICommandHandler
{
    [Option("event-path")]
    public string? EventPath { get; init; }

    [Option("repository")]
    public string? Repository { get; init; }

    [Option("summary-path")]
    public string? SummaryPath { get; init; }

    public async Task<int> RunAsync()
    {
        try
        {
            var eventPath = GitHubActionsEnvironment.GetRequired(EventPath, "GITHUB_EVENT_PATH");
            var repository = GitHubActionsEnvironment.GetRequired(Repository, "GITHUB_REPOSITORY");
            var summaryPath = string.IsNullOrWhiteSpace(SummaryPath)
                ? Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")
                : SummaryPath;
            var workflowEvent = GitHubWorkflowRunEvent.Read(eventPath, repository);
            if (!string.Equals(workflowEvent.EventName, "pull_request_target", StringComparison.Ordinal)
                || !string.Equals(
                    workflowEvent.RunRepository.FullName,
                    workflowEvent.Repository.FullName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Skip(
                    summaryPath,
                    "workflow_run did not originate from this repository pull_request_target workflow.");
            }

            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(BuilderResources.GitHubTokenRequired);
            }

            using var cancellationTokenSource = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationTokenSource.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;
            try
            {
                var service = new GitHubArtifactCommentService(token);
                var result = await service.RunAsync(workflowEvent, cancellationTokenSource.Token)
                    .ConfigureAwait(false);
                if (!result.Updated)
                {
                    return Skip(summaryPath, result.Message);
                }

                Log.Info(result.Message);
                GitHubActionsSummary.Write(
                    summaryPath,
                    "PR build artifact comment",
                    $"Updated PR #{result.PullRequestNumber} for workflow run {workflowEvent.RunId}, " +
                    $"attempt {result.RunAttempt}.");
                return 0;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (OperationCanceledException)
        {
            Log.Error("PR artifact comment was canceled.");
            return 130;
        }
        catch (Exception exception)
        {
            Log.Error(exception.Message);
            return 1;
        }
    }

    private static int Skip(string? summaryPath, string reason)
    {
        Log.Info($"Skipping PR comment: {reason}");
        GitHubActionsSummary.Write(
            summaryPath,
            "PR build artifact comment",
            $"Skipped: {GitHubArtifactCommentFormatter.EscapeMarkdown(reason)}");
        return 0;
    }
}

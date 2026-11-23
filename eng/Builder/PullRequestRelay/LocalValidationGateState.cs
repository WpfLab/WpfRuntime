namespace WpfReorganize.Builder;

internal sealed record LocalValidationGateState
{
    public required string Name { get; init; }

    public required string LogPath { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public int ExitCode { get; init; }
}
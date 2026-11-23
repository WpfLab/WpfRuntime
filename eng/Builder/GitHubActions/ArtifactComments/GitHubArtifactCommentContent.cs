namespace WpfReorganize.Builder;

internal sealed record GitHubArtifactCommentContent
(
    string Marker,
    string RunMarker,
    string Body,
    bool HasValidSuccessArtifacts,
    GitObjectId? TestedSha
);
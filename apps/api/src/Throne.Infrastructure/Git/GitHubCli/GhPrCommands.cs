namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Argument builders for the <c>gh api</c> calls that back the PR surface.
/// All calls use <c>-i</c> so the response status line and headers come
/// back on stdout — the ETag header is required for conditional GET (ADR-0024
/// § 4) and the status line is the only reliable signal for 304.
/// </summary>
internal static class GhPrCommands
{
    /// <summary><c>gh api -i /repos/{owner}/{repo}/pulls/{number}</c>.</summary>
    public static string[] GetPullRequest(string owner, string repo, int number) =>
        ["api", "-i", $"/repos/{owner}/{repo}/pulls/{number}"];

    /// <summary>
    /// <c>gh api -i [-H "If-None-Match: {etag}"] /repos/{owner}/{repo}/pulls/{number}/comments</c>.
    /// Inline review comments anchored to diff lines. ETag is added only when
    /// non-empty so an initial sync does not send an invalid header.
    /// </summary>
    public static string[] ListReviewComments(string owner, string repo, int number, string? etag) =>
        BuildApi($"/repos/{owner}/{repo}/pulls/{number}/comments", etag);

    /// <summary>
    /// <c>gh api -i [-H "If-None-Match: {etag}"] /repos/{owner}/{repo}/issues/{number}/comments</c>.
    /// Issue-comments feed of the same PR — GitHub stores discussion-thread
    /// comments under the underlying issue, not the pull-request endpoint.
    /// </summary>
    public static string[] ListIssueComments(string owner, string repo, int number, string? etag) =>
        BuildApi($"/repos/{owner}/{repo}/issues/{number}/comments", etag);

    private static string[] BuildApi(string path, string? etag) =>
        string.IsNullOrWhiteSpace(etag)
            ? ["api", "-i", path]
            : ["api", "-i", "-H", $"If-None-Match: {etag}", path];
}

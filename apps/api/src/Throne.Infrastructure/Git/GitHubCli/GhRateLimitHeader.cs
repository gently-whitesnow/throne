namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Helpers for the <c>X-RateLimit-Remaining</c> / <c>X-RateLimit-Reset</c>
/// header pair. Split out from <see cref="GhHttpStatus"/> so each helper class
/// stays inside the CA1502 cyclomatic budget.
/// </summary>
internal static class GhRateLimitHeader
{
    public static bool IsExhausted(Dictionary<string, string> headers, out DateTimeOffset? resetAt)
    {
        resetAt = null;
        if (!IsRemainingZero(headers))
        {
            return false;
        }

        resetAt = ReadResetAt(headers);
        return true;
    }

    private static bool IsRemainingZero(Dictionary<string, string> headers) =>
        headers.TryGetValue("X-RateLimit-Remaining", out var remaining)
            && int.TryParse(remaining, out var value)
            && value <= 0;

    private static DateTimeOffset? ReadResetAt(Dictionary<string, string> headers) =>
        headers.TryGetValue("X-RateLimit-Reset", out var resetRaw)
            && long.TryParse(resetRaw, out var epoch)
            ? DateTimeOffset.FromUnixTimeSeconds(epoch)
            : null;
}

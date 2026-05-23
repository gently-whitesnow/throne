namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Thin façade over the three header / status helpers used by the PR surface:
/// <see cref="GhStatusLineParser"/> (HTTP status code from the response start),
/// <see cref="GhRateLimitHeader"/> (rate-limit telemetry), and an <c>ETag</c>
/// reader. Single entry-point keeps <see cref="GhPrResponse"/> readable while
/// each helper stays inside the CA1502 per-type budget.
/// </summary>
internal static class GhHttpStatus
{
    public static int ParseStatusCode(string raw) =>
        GhStatusLineParser.ParseStatusCode(raw);

    public static string? ReadEtag(Dictionary<string, string> headers) =>
        headers.TryGetValue("ETag", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    public static bool TryReadRateLimitExhausted(
        Dictionary<string, string> headers,
        out DateTimeOffset? resetAt) =>
        GhRateLimitHeader.IsExhausted(headers, out resetAt);
}

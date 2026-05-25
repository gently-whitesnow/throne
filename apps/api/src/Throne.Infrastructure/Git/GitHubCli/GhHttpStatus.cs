namespace Throne.Infrastructure.Git.GitHubCli;

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

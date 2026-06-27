using System.Net;

namespace Throne.Infrastructure.TaskTrackers.Kaiten;

/// <summary>
/// Raised when Kaiten answers with a non-success status that the retry policy could not recover
/// (a non-retryable status, or retries exhausted). Carries the raw status and body so a caller — the
/// settings axis validating a token (401), the sync axis hitting a tariff wall (402) — can branch on
/// the failure without reparsing.
/// </summary>
internal sealed class KaitenApiException(HttpStatusCode statusCode, string? body)
    : Exception($"Kaiten API request failed: HTTP {(int)statusCode} ({Describe(statusCode)}).")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? Body { get; } = body;

    private static string Describe(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "invalid or missing token",
        HttpStatusCode.PaymentRequired => "blocked by tariff plan",
        HttpStatusCode.Forbidden => "insufficient permissions",
        HttpStatusCode.NotFound => "resource not found",
        HttpStatusCode.TooManyRequests => "rate limit exceeded",
        _ => status.ToString(),
    };
}

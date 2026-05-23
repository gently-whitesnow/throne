using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Post-response guards for the PR surface — rate-limit detection and the
/// «non-2xx → typed exception» mapping. Split out from
/// <see cref="GhPrResponse"/> so each helper class stays inside the CA1502
/// per-type cyclomatic budget.
/// </summary>
internal static class GhPrResponseGuard
{
    public static void ThrowIfRateLimited(string operation, Dictionary<string, string> headers)
    {
        if (!GhHttpStatus.TryReadRateLimitExhausted(headers, out var resetAt))
        {
            return;
        }

        var reset = resetAt is null ? "unknown" : resetAt.Value.ToString("u");
        throw new GitProviderException(
            GitProviderErrorKind.NetworkError,
            $"gh {operation} hit GitHub rate limit (resets at {reset}).",
            detail: resetAt is null ? null : $"X-RateLimit-Reset={resetAt.Value.ToUnixTimeSeconds()}");
    }

    public static void ThrowIfNotSuccess(string operation, ProcessRunResult result, int status)
    {
        if (result.IsSuccess && status is >= 200 and < 300)
        {
            return;
        }

        throw GhExceptions.FromExit(operation, result);
    }
}

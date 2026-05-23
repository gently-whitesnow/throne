using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Maps <c>gh</c> stderr to <see cref="GitProviderErrorKind"/>. The CLI does not
/// expose machine-readable error codes, so we sniff stable substrings from
/// <c>gh</c> 2.x. Kept tiny on purpose: only the categories that change caller
/// behaviour per ADR-0024 § 5/7 (NotFound → broken, AuthFailed → settings page,
/// NetworkError → retryable). Patterns live in a table to keep the dispatcher
/// inside the CA1502 cyclomatic budget.
/// </summary>
internal static class GhErrorClassifier
{
    private static readonly (string Needle, GitProviderErrorKind Kind)[] Patterns =
    {
        ("HTTP 404", GitProviderErrorKind.NotFound),
        ("Not Found", GitProviderErrorKind.NotFound),
        ("Could not resolve to a Repository", GitProviderErrorKind.NotFound),
        ("HTTP 401", GitProviderErrorKind.AuthFailed),
        ("HTTP 403", GitProviderErrorKind.AuthFailed),
        ("authentication required", GitProviderErrorKind.AuthFailed),
        ("not logged into", GitProviderErrorKind.AuthFailed),
        ("Bad credentials", GitProviderErrorKind.AuthFailed),
        ("gh auth login", GitProviderErrorKind.AuthFailed),
        ("You are not authorized", GitProviderErrorKind.AuthFailed),
        ("HTTP 5", GitProviderErrorKind.NetworkError),
        ("timeout", GitProviderErrorKind.NetworkError),
        ("temporary failure", GitProviderErrorKind.NetworkError),
        ("Could not resolve host", GitProviderErrorKind.NetworkError),
        ("connection refused", GitProviderErrorKind.NetworkError),
        ("TLS handshake", GitProviderErrorKind.NetworkError),
        ("network is unreachable", GitProviderErrorKind.NetworkError),
        ("rate limit", GitProviderErrorKind.NetworkError),
    };

    public static GitProviderErrorKind Classify(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return GitProviderErrorKind.CliFailure;
        }

        foreach (var (needle, kind) in Patterns)
        {
            if (stderr.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return GitProviderErrorKind.CliFailure;
    }

    /// <summary>Render stderr as a single line for log / exception messages.</summary>
    public static string OneLine(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return "<no stderr output>";
        }

        var compact = stderr.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return compact.Length > 240 ? string.Concat(compact.AsSpan(0, 237), "...") : compact;
    }
}

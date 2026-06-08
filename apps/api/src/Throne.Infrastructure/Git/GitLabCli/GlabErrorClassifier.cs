using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabErrorClassifier
{
    private static readonly (string Needle, GitProviderErrorKind Kind)[] Patterns =
    {
        ("404", GitProviderErrorKind.NotFound),
        ("Not Found", GitProviderErrorKind.NotFound),
        ("401", GitProviderErrorKind.AuthFailed),
        ("403", GitProviderErrorKind.AuthFailed),
        ("authentication required", GitProviderErrorKind.AuthFailed),
        ("not authenticated", GitProviderErrorKind.AuthFailed),
        ("auth login", GitProviderErrorKind.AuthFailed),
        ("HTTP 5", GitProviderErrorKind.NetworkError),
        ("Service Unavailable", GitProviderErrorKind.NetworkError),
        ("Bad Gateway", GitProviderErrorKind.NetworkError),
        ("Gateway Timeout", GitProviderErrorKind.NetworkError),
        ("timeout", GitProviderErrorKind.NetworkError),
        ("temporary failure", GitProviderErrorKind.NetworkError),
        ("Could not resolve host", GitProviderErrorKind.NetworkError),
        ("connection refused", GitProviderErrorKind.NetworkError),
        ("connection reset", GitProviderErrorKind.NetworkError),
        ("TLS handshake", GitProviderErrorKind.NetworkError),
        ("certificate", GitProviderErrorKind.NetworkError),
        ("network is unreachable", GitProviderErrorKind.NetworkError),
        ("no route to host", GitProviderErrorKind.NetworkError),
        ("proxyconnect", GitProviderErrorKind.NetworkError),
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

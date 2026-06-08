using System.Text.Json;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Builds <see cref="ProviderAuthStatus"/> from a <c>gh api user -i</c> response.
/// We prefer this over <c>gh auth status</c> because:
/// 1) It is one round-trip that simultaneously proves the token works,
/// 2) Response headers carry <c>X-OAuth-Scopes</c> — needed by the settings page
///    to flag «missing repo scope»,
/// 3) Body carries <c>login</c> verbatim, no parsing of <c>gh auth status</c>'s
///    human-readable output.
/// </summary>
internal static class GhAuthStatusParser
{
    public static ProviderAuthStatus ParseUserResponse(ProcessRunResult userCall, string host)
    {
        ArgumentNullException.ThrowIfNull(userCall);

        if (!userCall.IsSuccess)
        {
            var kind = GhErrorClassifier.Classify(userCall.StandardError);
            return new ProviderAuthStatus(
                Provider: GitProviderNames.GitHub,
                IsAuthenticated: false,
                Host: host,
                Detail: GhErrorClassifier.OneLine(userCall.StandardError),
                State: StateFromError(kind));
        }

        var split = GhHttpResponseSplitter.Split(userCall.StandardOutput);
        var login = TryReadLogin(split.Body);
        var scopes = GhScopeReader.Read(split.Headers);
        return new ProviderAuthStatus(
            Provider: GitProviderNames.GitHub,
            IsAuthenticated: !string.IsNullOrEmpty(login),
            Account: login,
            Host: host,
            Detail: scopes.Detail,
            State: string.IsNullOrEmpty(login)
                ? ProviderAuthStateNames.Unauthenticated
                : ProviderAuthStateNames.Authenticated)
        {
            Scopes = scopes.Values,
        };
    }

    private static string StateFromError(GitProviderErrorKind kind) => kind switch
    {
        GitProviderErrorKind.NetworkError => ProviderAuthStateNames.Offline,
        GitProviderErrorKind.CliFailure => ProviderAuthStateNames.Missing,
        _ => ProviderAuthStateNames.Unauthenticated,
    };

    private static string? TryReadLogin(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            return GhJson.String(doc.RootElement, "login");
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

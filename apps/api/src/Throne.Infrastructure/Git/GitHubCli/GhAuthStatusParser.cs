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
            return new ProviderAuthStatus(
                Provider: GitProviderNames.GitHub,
                IsAuthenticated: false,
                Host: host,
                Detail: GhErrorClassifier.OneLine(userCall.StandardError));
        }

        var split = GhHttpResponseSplitter.Split(userCall.StandardOutput);
        var login = TryReadLogin(split.Body);
        return new ProviderAuthStatus(
            Provider: GitProviderNames.GitHub,
            IsAuthenticated: !string.IsNullOrEmpty(login),
            Account: login,
            Host: host,
            Detail: BuildScopeDetail(split.Headers));
    }

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

    private static string? BuildScopeDetail(Dictionary<string, string> headers) =>
        headers.TryGetValue("X-OAuth-Scopes", out var scopes) && !string.IsNullOrWhiteSpace(scopes)
            ? $"scopes: {scopes}"
            : null;
}

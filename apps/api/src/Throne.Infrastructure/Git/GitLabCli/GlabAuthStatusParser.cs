using System.Text.Json;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabAuthStatusParser
{
    public static ProviderAuthStatus ParseUserResponse(ProcessRunResult userCall, string host)
    {
        ArgumentNullException.ThrowIfNull(userCall);

        if (!userCall.IsSuccess)
        {
            var kind = GlabErrorClassifier.Classify(userCall.StandardError);
            return new ProviderAuthStatus(
                Provider: GitProviderNames.GitLab,
                IsAuthenticated: false,
                Host: host,
                Detail: GlabErrorClassifier.OneLine(userCall.StandardError),
                State: StateFromError(kind));
        }

        var split = GhHttpResponseSplitter.Split(userCall.StandardOutput);
        var username = TryReadUsername(split.Body);
        return new ProviderAuthStatus(
            Provider: GitProviderNames.GitLab,
            IsAuthenticated: !string.IsNullOrEmpty(username),
            Account: username,
            Host: host,
            State: string.IsNullOrEmpty(username)
                ? ProviderAuthStateNames.Unauthenticated
                : ProviderAuthStateNames.Authenticated);
    }

    private static string? TryReadUsername(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("username", out var username)
                && username.ValueKind == JsonValueKind.String
                    ? username.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string StateFromError(GitProviderErrorKind kind) => kind switch
    {
        GitProviderErrorKind.NetworkError => ProviderAuthStateNames.Offline,
        GitProviderErrorKind.CliFailure => ProviderAuthStateNames.Missing,
        _ => ProviderAuthStateNames.Unauthenticated,
    };
}

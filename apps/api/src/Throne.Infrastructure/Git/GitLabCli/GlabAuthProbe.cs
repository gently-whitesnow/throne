using Microsoft.Extensions.Options;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitLabCli;

internal sealed class GlabAuthProbe(GlabCliInvoker glab, IOptions<GitLabSettings> settings)
{
    public async Task<ProviderAuthStatus> ProbeAsync(CancellationToken ct)
    {
        var host = settings.Value.Host?.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return new ProviderAuthStatus(
                Provider: GitProviderNames.GitLab,
                IsAuthenticated: false,
                Detail: "Throne:GitLab:Host is not configured.",
                State: ProviderAuthStateNames.Missing);
        }

        var version = await RunVersionAsync(host, ct);
        if (!version.IsAuthenticated)
        {
            return version;
        }

        try
        {
            var result = await glab.RunAsync(["api", "user", "--hostname", host, "-i"], GlabEnvironment.ForHost(host), ct);
            return GlabAuthStatusParser.ParseUserResponse(result, host);
        }
        catch (GitProviderException ex)
        {
            return FromException(host, ex);
        }
    }

    private async Task<ProviderAuthStatus> RunVersionAsync(string host, CancellationToken ct)
    {
        try
        {
            var result = await glab.RunAsync(["version"], ct);
            if (result.IsSuccess)
            {
                return new ProviderAuthStatus(GitProviderNames.GitLab, true, Host: host);
            }

            return new ProviderAuthStatus(
                Provider: GitProviderNames.GitLab,
                IsAuthenticated: false,
                Host: host,
                Detail: GlabErrorClassifier.OneLine(result.StandardError),
                State: ProviderAuthStateNames.Missing);
        }
        catch (GitProviderException ex)
        {
            return FromException(host, ex);
        }
    }

    private static ProviderAuthStatus FromException(string host, GitProviderException ex) =>
        new(
            Provider: GitProviderNames.GitLab,
            IsAuthenticated: false,
            Host: host,
            Detail: ex.Message,
            State: StateFromError(ex.Kind));

    private static string StateFromError(GitProviderErrorKind kind) => kind switch
    {
        GitProviderErrorKind.NetworkError => ProviderAuthStateNames.Offline,
        GitProviderErrorKind.CliFailure => ProviderAuthStateNames.Missing,
        _ => ProviderAuthStateNames.Unauthenticated,
    };
}

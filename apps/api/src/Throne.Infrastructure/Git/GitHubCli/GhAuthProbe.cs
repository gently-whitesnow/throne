using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Runs the <c>gh api user -i</c> probe and folds CLI / network failures into
/// a non-authenticated <see cref="ProviderAuthStatus"/> so the settings page
/// never observes a thrown <see cref="GitProviderException"/>.
/// </summary>
internal sealed class GhAuthProbe(GhCliInvoker gh)
{
    private const string DefaultHost = "github.com";

    public async Task<ProviderAuthStatus> ProbeAsync(CancellationToken ct)
    {
        ProcessRunResult result;
        try
        {
            result = await gh.RunAsync(["api", "user", "-i"], ct);
        }
        catch (GitProviderException ex) when (IsSwallowable(ex.Kind))
        {
            return new ProviderAuthStatus(
                Provider: GitProviderNames.GitHub,
                IsAuthenticated: false,
                Host: DefaultHost,
                Detail: ex.Message);
        }

        return GhAuthStatusParser.ParseUserResponse(result, DefaultHost);
    }

    private static bool IsSwallowable(GitProviderErrorKind kind) =>
        kind is GitProviderErrorKind.CliFailure or GitProviderErrorKind.NetworkError;
}

using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Read-only proxy that fetches PR review comments straight from the binding's
/// provider. The server is pointer-only — the HTTP GET path used by the UI never
/// touches persistence. The binding's stored etag is intentionally not used here:
/// GET must not mutate the background poller's cursor, and a 304 leaves the caller
/// without comment bodies.
/// </summary>
public sealed class ListPullRequestCommentsUseCase(IGitProviderRegistry providers)
{
    public async Task<IReadOnlyList<PullRequestComment>> ListByBindingAsync(
        IntentRepositoryBinding binding,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.State.PullRequestNumber is null)
        {
            throw RepositoryBindingFailures.PullRequestNotAttached(binding);
        }
        var provider = providers.GetByName(binding.Coordinate.Provider)
            ?? throw RepositoryBindingFailures.ProviderUnsupported(binding.Coordinate.Provider);
        var page = await provider.ListPullRequestCommentsAsync(
            owner: binding.Coordinate.Owner,
            repo: binding.Coordinate.Repo,
            number: binding.State.PullRequestNumber.Value,
            etag: null,
            ct: ct) ?? throw RepositoryBindingFailures.UpstreamGone(binding);
        // GET never sends If-None-Match (etag intentionally null above), so the
        // provider always returns a Fresh page on success.
        return page is PullRequestCommentsPage.Fresh fresh
            ? fresh.Comments
            : [];
    }
}

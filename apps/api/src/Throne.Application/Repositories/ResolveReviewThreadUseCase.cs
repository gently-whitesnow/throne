using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Toggles a review thread's resolution state straight at the provider — no
/// durable storage on Throne's side. Maps provider 404 into
/// <see cref="RepositoryBindingFailures.UpstreamGone"/> (404) and the unsupported /
/// missing-thread cases into a 422 the HTTP layer renders.
/// </summary>
public sealed class ResolveReviewThreadUseCase(IGitProviderRegistry providers)
{
    public async Task<ReviewThreadState> ResolveAsync(
        IntentRepositoryBinding binding,
        string threadId,
        bool resolved,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        if (binding.State.PullRequestNumber is null)
        {
            throw RepositoryBindingFailures.PullRequestNotAttached(binding);
        }
        var provider = providers.GetByName(binding.Coordinate.Provider)
            ?? throw RepositoryBindingFailures.ProviderUnsupported(binding.Coordinate.Provider);

        try
        {
            return await provider.ResolveReviewThreadAsync(
                binding.Coordinate.Owner,
                binding.Coordinate.Repo,
                binding.State.PullRequestNumber.Value,
                threadId,
                resolved,
                ct);
        }
        catch (GitProviderException ex) when (ex.Kind == GitProviderErrorKind.NotFound)
        {
            throw RepositoryBindingFailures.UpstreamGone(binding);
        }
        catch (GitProviderException ex) when (ex.Kind == GitProviderErrorKind.ReviewCommentAnchorInvalid)
        {
            throw RepositoryBindingFailures.ReviewAnchorInvalid(binding, ex);
        }
    }
}

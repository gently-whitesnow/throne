using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Submits an inline review comment straight to the provider — no durable storage.
/// Maps <see cref="GitProviderErrorKind.ReviewCommentAnchorInvalid"/> into a typed
/// <see cref="ApiException"/> the HTTP layer renders as 422.
/// </summary>
public sealed class SubmitReviewWorkspaceCommentUseCase(IGitProviderRegistry providers)
{
    public async Task<SubmittedReviewComment> SubmitAsync(
        IntentRepositoryBinding binding,
        SubmitReviewCommentRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(request);
        if (binding.State.PullRequestNumber is null)
        {
            throw RepositoryBindingFailures.PullRequestNotAttached(binding);
        }
        var provider = providers.GetByName(binding.Coordinate.Provider)
            ?? throw RepositoryBindingFailures.ProviderUnsupported(binding.Coordinate.Provider);

        try
        {
            return await provider.SubmitReviewCommentAsync(
                binding.Coordinate.Owner,
                binding.Coordinate.Repo,
                binding.State.PullRequestNumber.Value,
                request,
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

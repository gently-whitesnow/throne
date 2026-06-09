using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Deletes a single inline review comment straight at the provider — no durable
/// storage on Throne's side. GitHub deletes by comment id alone; GitLab needs the
/// owning <paramref name="threadId"/> (discussion id) and surfaces its absence as a
/// 422 via <see cref="RepositoryBindingFailures.ReviewAnchorInvalid"/>.
/// </summary>
public sealed class DeletePullRequestCommentUseCase(IGitProviderRegistry providers)
{
    public async Task DeleteAsync(
        IntentRepositoryBinding binding,
        string commentId,
        string? threadId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(commentId);
        if (binding.State.PullRequestNumber is null)
        {
            throw RepositoryBindingFailures.PullRequestNotAttached(binding);
        }
        var provider = providers.GetByName(binding.Coordinate.Provider)
            ?? throw RepositoryBindingFailures.ProviderUnsupported(binding.Coordinate.Provider);

        try
        {
            await provider.DeleteReviewCommentAsync(
                binding.Coordinate.Owner,
                binding.Coordinate.Repo,
                binding.State.PullRequestNumber.Value,
                commentId,
                threadId,
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

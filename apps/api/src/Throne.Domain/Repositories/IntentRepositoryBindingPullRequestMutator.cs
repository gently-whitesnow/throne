namespace Throne.Domain.Repositories;

/// <summary>
/// Pull-request / sync operations for <see cref="IntentRepositoryBinding"/>. Separated from
/// <see cref="IntentRepositoryBindingMutator"/> so each type stays within CA1502 cyclomatic
/// budget.
/// </summary>
public static class IntentRepositoryBindingPullRequestMutator
{
    public static void AttachPullRequest(this IntentRepositoryBinding binding, int number, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Pull request number must be >= 1.");
        }

        if (binding.State.PullRequestNumber is not null)
        {
            throw new InvalidOperationException(
                $"Pull request already attached to binding {binding.Id}: #{binding.State.PullRequestNumber}.");
        }

        binding.State = binding.State with { PullRequestNumber = number, UpdatedAt = at };
    }

    public static void RecordPullRequestState(this IntentRepositoryBinding binding, string state, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        if (binding.State.PullRequestNumber is null)
        {
            throw new InvalidOperationException(
                $"Cannot record pull request state on binding {binding.Id}: no PR attached.");
        }

        if (!PullRequestStateNames.IsKnown(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), $"Unknown pull request state: {state}.");
        }

        binding.State = binding.State with { PullRequestState = state, UpdatedAt = at };
    }

    public static void RecordSync(
        this IntentRepositoryBinding binding,
        string? etag,
        DateTimeOffset? lastSeenReviewCommentAt,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(binding);
        // Review D5 — sanitize through ReviewCommentsEtagNormalizer (RFC 7230
        // visible ASCII). Anything off-spec collapses to null so the next poll
        // does a full fetch instead of echoing an unsafe If-None-Match header.
        var prevCursor = binding.State.LastSeenReviewCommentAt;
        var nextCursor = prevCursor is null
            ? lastSeenReviewCommentAt
            : lastSeenReviewCommentAt is null
                ? prevCursor
                : lastSeenReviewCommentAt > prevCursor ? lastSeenReviewCommentAt : prevCursor;
        binding.State = binding.State with
        {
            ReviewCommentsEtag = ReviewCommentsEtagNormalizer.Normalize(etag),
            LastSeenReviewCommentAt = nextCursor,
            LastSyncedAt = at,
            UpdatedAt = at,
        };
    }
}

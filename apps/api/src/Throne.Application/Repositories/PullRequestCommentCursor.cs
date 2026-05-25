using Throne.Application.Git;

namespace Throne.Application.Repositories;

/// <summary>
/// Pure helpers for the pointer-only sync workflow (intent
/// 9aa2c64ff2a94410b7352eada1350ad0). Keeps the cyclomatic budget on
/// <see cref="RepositoryPullRequestSyncPersistence"/> within CA1502 limits by
/// hosting the «what counts as new» and «advance the cursor» decisions here.
/// </summary>
internal static class PullRequestCommentCursor
{
    public static IReadOnlyList<PullRequestComment> FilterNew(
        IReadOnlyList<PullRequestComment> comments,
        DateTimeOffset? lastSeenAt)
    {
        if (comments.Count == 0)
        {
            return [];
        }
        // Initial sync (cursor is null) emits SSE for the full backlog — matches the
        // slice 1 behaviour, where an empty Mongo collection meant all upstream
        // entries counted as new.
        if (lastSeenAt is null)
        {
            return comments;
        }
        return comments.Where(c => c.CreatedAt > lastSeenAt.Value).ToList();
    }

    public static DateTimeOffset? Advance(
        DateTimeOffset? lastSeenAt,
        IReadOnlyList<PullRequestComment> newComments)
    {
        if (newComments.Count == 0)
        {
            return lastSeenAt;
        }
        var max = newComments.Max(c => c.CreatedAt);
        return lastSeenAt is null || max > lastSeenAt.Value ? max : lastSeenAt;
    }
}

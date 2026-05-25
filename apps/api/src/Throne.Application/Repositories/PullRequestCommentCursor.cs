using Throne.Application.Git;

namespace Throne.Application.Repositories;

/// <summary>
/// Pure helpers for the pointer-only sync workflow: «what counts as new» and
/// «advance the cursor» decisions.
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
        // Initial sync (cursor is null) emits SSE for the full backlog — matches
        // the historical behaviour where an empty store meant all upstream entries
        // counted as new.
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

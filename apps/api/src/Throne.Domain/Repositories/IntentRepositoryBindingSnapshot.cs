using Throne.Domain.Intents;

namespace Throne.Domain.Repositories;

/// <summary>
/// Persistence-shaped snapshot used by <see cref="IntentRepositoryBindingFactory.Restore"/>.
/// </summary>
public sealed record IntentRepositoryBindingSnapshot(
    BindingId Id,
    IntentId IntentId,
    RepoCoordinate Coordinate,
    string DefaultBranch,
    string WorkspacePath,
    string CloneStatus,
    string? CloneError,
    int? PullRequestNumber,
    string? PullRequestState,
    string? ReviewCommentsEtag,
    DateTimeOffset? LastSeenReviewCommentAt,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

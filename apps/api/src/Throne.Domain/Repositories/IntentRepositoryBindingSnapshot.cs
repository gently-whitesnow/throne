using Throne.Domain.Intents;

namespace Throne.Domain.Repositories;

/// <summary>
/// Persistence-shaped snapshot used by <see cref="IntentRepositoryBindingFactory.Restore"/>.
/// Keeps the rehydration entry-point param list trivial so callers (T-04 mongo mapper) and
/// CA1502 cyclomatic budgets both stay comfortable.
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
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

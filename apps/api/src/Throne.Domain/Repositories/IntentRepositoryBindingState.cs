namespace Throne.Domain.Repositories;

/// <summary>
/// Mutable portion of <see cref="IntentRepositoryBinding"/> grouped into a record so the
/// aggregate's constructor stays trivial and CA1502 cyclomatic budgets are respected.
/// </summary>
public sealed record IntentRepositoryBindingState(
    string DefaultBranch,
    string CloneStatus,
    string? CloneError,
    int? PullRequestNumber,
    string? PullRequestState,
    string? ReviewCommentsEtag,
    DateTimeOffset? LastSeenReviewCommentAt,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset UpdatedAt);

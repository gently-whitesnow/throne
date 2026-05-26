namespace Throne.Domain.Repositories;

/// <summary>
/// Mutable portion of <see cref="IntentRepositoryBinding"/> grouped into a record so
/// state transitions can be expressed as <c>State = State with { … }</c> inside the
/// aggregate's instance methods.
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

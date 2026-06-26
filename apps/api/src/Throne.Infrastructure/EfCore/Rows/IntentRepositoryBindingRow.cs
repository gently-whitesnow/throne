namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>intent_repository_bindings</c> table (ADR-0024).
/// Mirrors <c>IntentRepositoryBindingDocument</c>; <c>host</c> is the EFFECTIVE host
/// (RepoCoordinate-normalized GitHub default → "github.com") so the unique
/// <c>(intent_id, provider, host, owner, repo)</c> index has no NULL slot.
/// </summary>
internal sealed class IntentRepositoryBindingRow
{
    public string Id { get; set; } = string.Empty;
    public string IntentId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    public string DefaultBranch { get; set; } = string.Empty;
    public string WorkspacePath { get; set; } = string.Empty;
    public string CloneStatus { get; set; } = string.Empty;
    public string? CloneError { get; set; }
    public int? PullRequestNumber { get; set; }
    public string? PullRequestState { get; set; }
    public string? ReviewCommentsEtag { get; set; }
    public DateTimeOffset? LastSeenReviewCommentAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool SuppressMergeAutoClose { get; set; }
}

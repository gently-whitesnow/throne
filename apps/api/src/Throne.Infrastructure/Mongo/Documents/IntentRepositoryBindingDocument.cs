using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Wire shape of <see cref="Throne.Domain.Repositories.IntentRepositoryBinding"/> in the
/// <c>intent_repository_bindings</c> collection. Field names mirror the OpenAPI DTO in
/// <c>specs/contracts/repositories/openapi.yaml</c> so a Mongo dump can be read 1:1 against
/// the public contract.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed class IntentRepositoryBindingDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [BsonElement("provider")]
    public string Provider { get; set; } = string.Empty;

    [BsonElement("host")]
    public string? Host { get; set; }

    [BsonElement("owner")]
    public string Owner { get; set; } = string.Empty;

    [BsonElement("repo")]
    public string Repo { get; set; } = string.Empty;

    [BsonElement("project_id")]
    public int? ProjectId { get; set; }

    [BsonElement("default_branch")]
    public string DefaultBranch { get; set; } = string.Empty;

    [BsonElement("workspace_path")]
    public string WorkspacePath { get; set; } = string.Empty;

    [BsonElement("clone_status")]
    public string CloneStatus { get; set; } = string.Empty;

    [BsonElement("clone_error")]
    public string? CloneError { get; set; }

    [BsonElement("pull_request_number")]
    public int? PullRequestNumber { get; set; }

    [BsonElement("pull_request_state")]
    public string? PullRequestState { get; set; }

    [BsonElement("review_comments_etag")]
    public string? ReviewCommentsEtag { get; set; }

    [BsonElement("last_seen_review_comment_at")]
    public DateTime? LastSeenReviewCommentAt { get; set; }

    [BsonElement("last_synced_at")]
    public DateTime? LastSyncedAt { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Absent on documents written before the keep-session-on-merge option (ADR-0024 surface):
    // the BSON default of false reproduces the original auto-close-on-merge behaviour.
    [BsonElement("suppress_merge_auto_close")]
    public bool SuppressMergeAutoClose { get; set; }
}

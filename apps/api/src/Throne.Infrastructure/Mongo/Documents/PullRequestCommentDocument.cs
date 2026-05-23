using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Wire shape of <see cref="Throne.Domain.Repositories.PullRequestCommentRecord"/> in
/// the <c>pull_request_comments</c> collection (ADR-0024 § 6, T-10). Field names mirror
/// the OpenAPI <c>PullRequestCommentDto</c> so a Mongo dump reads 1:1 against the
/// public contract.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed class PullRequestCommentDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("binding_id")]
    public string BindingId { get; set; } = string.Empty;

    [BsonElement("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [BsonElement("upstream_id")]
    public string UpstreamId { get; set; } = string.Empty;

    [BsonElement("author_login")]
    public string AuthorLogin { get; set; } = string.Empty;

    [BsonElement("author_avatar_url")]
    public string? AuthorAvatarUrl { get; set; }

    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("html_url")]
    public string? HtmlUrl { get; set; }

    [BsonElement("path")]
    public string? Path { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [BsonElement("observed_at")]
    public DateTime ObservedAt { get; set; }
}

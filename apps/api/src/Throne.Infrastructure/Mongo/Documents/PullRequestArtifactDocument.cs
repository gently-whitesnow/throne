using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>Current-state document for ADR-0031 pull request artifacts.</summary>
[BsonIgnoreExtraElements]
internal sealed class PullRequestArtifactDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("binding_id")]
    public string BindingId { get; set; } = string.Empty;

    [BsonElement("pull_request_number")]
    public int PullRequestNumber { get; set; }

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("render")]
    public string Render { get; set; } = string.Empty;

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("summary")]
    public string Summary { get; set; } = string.Empty;

    [BsonElement("source")]
    public string Source { get; set; } = string.Empty;

    [BsonElement("source_refs")]
    public IReadOnlyList<string> SourceRefs { get; set; } = [];

    [BsonElement("produced_at")]
    public DateTime ProducedAt { get; set; }

    [BsonElement("head_sha")]
    [BsonIgnoreIfNull]
    public string? HeadSha { get; set; }

    [BsonElement("review_recommendation")]
    [BsonIgnoreIfNull]
    public ReviewRecommendationDocument? ReviewRecommendation { get; set; }
}

[BsonIgnoreExtraElements]
internal sealed class ReviewRecommendationDocument
{
    [BsonElement("file_order")]
    public IReadOnlyList<ReviewFileOrderEntryDocument> FileOrder { get; set; } = [];
}

[BsonIgnoreExtraElements]
internal sealed class ReviewFileOrderEntryDocument
{
    [BsonElement("path")]
    public string Path { get; set; } = string.Empty;

    [BsonElement("reason")]
    [BsonIgnoreIfNull]
    public string? Reason { get; set; }

    [BsonElement("risk")]
    [BsonIgnoreIfNull]
    public string? Risk { get; set; }
}

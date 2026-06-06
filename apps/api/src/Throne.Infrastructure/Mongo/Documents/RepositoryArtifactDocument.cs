using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Wire shape of <see cref="Throne.Domain.Repositories.RepositoryArtifact"/> in the
/// <c>repository_artifacts</c> collection (ADR-0031). The current state of a knowledge page;
/// its history lives in <see cref="RepositoryArtifactVersionDocument"/>.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed class RepositoryArtifactDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("provider")]
    public string Provider { get; set; } = string.Empty;

    [BsonElement("owner")]
    public string Owner { get; set; } = string.Empty;

    [BsonElement("repo")]
    public string Repo { get; set; } = string.Empty;

    [BsonElement("slug")]
    public string Slug { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("document")]
    public string Document { get; set; } = string.Empty;

    [BsonElement("render_hint")]
    public string RenderHint { get; set; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

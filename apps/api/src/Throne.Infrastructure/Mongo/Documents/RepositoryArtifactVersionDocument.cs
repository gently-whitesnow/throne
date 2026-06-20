using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Wire shape of <see cref="Throne.Domain.Repositories.RepositoryArtifactVersion"/> in the
/// append-only <c>repository_artifact_versions</c> collection (ADR-0031): a full snapshot of
/// a knowledge page at one version, keyed unique on <c>(artifact_id, version)</c>.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed class RepositoryArtifactVersionDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("artifact_id")]
    public string ArtifactId { get; set; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("document")]
    public string Document { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}

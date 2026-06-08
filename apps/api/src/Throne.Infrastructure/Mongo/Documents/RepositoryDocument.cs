using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Wire shape of <see cref="Throne.Domain.Repositories.Repository"/> in the
/// <c>repositories</c> collection (ADR-0031). Thin registry row keyed on the unique
/// <c>(provider, owner, repo)</c> coordinate.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed class RepositoryDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

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

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

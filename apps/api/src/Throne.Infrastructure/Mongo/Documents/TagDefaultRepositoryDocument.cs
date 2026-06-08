using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Subdocument inside <see cref="TagDocument"/>.<c>default_repositories</c>. Field
/// shape mirrors the OpenAPI <c>TagDefaultRepositoryDto</c> 1:1 so a Mongo dump
/// reads cleanly against the public contract.
/// </summary>
internal sealed class TagDefaultRepositoryDocument
{
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
    public string? DefaultBranch { get; set; }
}

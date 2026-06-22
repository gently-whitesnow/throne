using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Singleton document in the <see cref="MongoCollectionNames.GitLabHostSettings"/>
/// collection (<c>_id = "singleton"</c>). Holds the operator-configured GitLab hostname
/// used by <c>glab</c>. Default <c>gitlab.com</c> is materialised on first read.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed class GitLabHostDocument
{
    public const string SingletonId = "singleton";

    [BsonId]
    public string Id { get; set; } = SingletonId;

    [BsonElement("host")]
    public string Host { get; set; } = string.Empty;
}

using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

[BsonIgnoreExtraElements]
internal sealed class PersonalAccessTokenDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("owner_user_id")]
    public string OwnerUserId { get; set; } = string.Empty;

    [BsonElement("hash_sha256")]
    public string HashSha256 { get; set; } = string.Empty;

    [BsonElement("last_four")]
    public string LastFour { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}

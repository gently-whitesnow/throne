using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

[BsonIgnoreExtraElements]
internal sealed class IntentAttachmentDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [BsonElement("gridfs_id")]
    public string GridFsId { get; set; } = string.Empty;

    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement("size_bytes")]
    public long SizeBytes { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("compression_state")]
    [BsonIgnoreIfNull]
    public string? CompressionState { get; set; }

    [BsonElement("derived_width")]
    [BsonIgnoreIfNull]
    public int? DerivedWidth { get; set; }

    [BsonElement("derived_height")]
    [BsonIgnoreIfNull]
    public int? DerivedHeight { get; set; }
}

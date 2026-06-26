using MongoDB.Bson;

namespace Throne.MigrateMongoSqlite;

internal static class AttachmentRows
{
    public const string SourceCollection = "intent_attachments";
    public const string TargetTable = "intent_attachments";

    public static IReadOnlyList<ColumnValue> ReadValues(BsonDocument document, byte[] contentBytes) =>
    [
        new("id", BsonFields.Id(document)),
        new("intent_id", BsonFields.String(document, "intent_id")),
        new("file_name", BsonFields.String(document, "file_name")),
        new("content_type", BsonFields.String(document, "content_type")),
        new("size_bytes", BsonFields.Int64(document, "size_bytes")),
        new("created_at", BsonFields.DateTimeText(document, "created_at")),
        new("compression_state", BsonFields.NullableString(document, "compression_state")),
        new("derived_width", BsonFields.NullableInt32(document, "derived_width")),
        new("derived_height", BsonFields.NullableInt32(document, "derived_height")),
        new("content_bytes", contentBytes),
    ];

    public static ObjectId ReadGridFsObjectId(BsonDocument document)
    {
        var value = BsonFields.ValueOrNull(document, "gridfs_id")
            ?? throw new InvalidOperationException(
                $"Attachment {BsonFields.Id(document)} has no gridfs_id.");

        if (value.BsonType == BsonType.ObjectId)
        {
            return value.AsObjectId;
        }
        if (value.BsonType == BsonType.String && ObjectId.TryParse(value.AsString, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Attachment {BsonFields.Id(document)} has unsupported gridfs_id '{value}'.");
    }
}

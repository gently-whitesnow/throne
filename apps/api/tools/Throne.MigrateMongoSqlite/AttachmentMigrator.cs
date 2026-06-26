using Microsoft.Data.Sqlite;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace Throne.MigrateMongoSqlite;

internal sealed class AttachmentMigrator(IMongoDatabase database, string bucketName)
{
    private readonly GridFSBucket _bucket = new(database, new GridFSBucketOptions { BucketName = bucketName });
    private readonly IMongoCollection<BsonDocument> _metadata =
        database.GetCollection<BsonDocument>(AttachmentRows.SourceCollection);

    public async Task<int> CopyAsync(
        SqliteWriter writer,
        SqliteTransaction transaction,
        CancellationToken ct)
    {
        var count = 0;
        using var cursor = await _metadata.Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync(ct);
        while (await cursor.MoveNextAsync(ct))
        {
            foreach (var document in cursor.Current)
            {
                var content = await ReadContentAsync(document, ct);
                await writer.InsertAsync(
                    AttachmentRows.TargetTable,
                    AttachmentRows.ReadValues(document, content),
                    transaction,
                    ct);
                count++;
            }
        }
        return count;
    }

    private async Task<byte[]> ReadContentAsync(BsonDocument document, CancellationToken ct)
    {
        var gridFsId = AttachmentRows.ReadGridFsObjectId(document);
        try
        {
            using var stream = await _bucket.OpenDownloadStreamAsync(gridFsId, cancellationToken: ct);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }
        catch (GridFSFileNotFoundException ex)
        {
            throw new InvalidOperationException(
                $"GridFS file {gridFsId} for attachment {BsonFields.Id(document)} was not found.",
                ex);
        }
    }
}

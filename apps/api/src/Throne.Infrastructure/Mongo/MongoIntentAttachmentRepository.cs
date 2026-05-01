using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentAttachmentRepository(IMongoDatabase database) : IIntentAttachmentRepository
{
    private const string GridFsBucketName = "intent_attachment_fs";

    private readonly IMongoCollection<IntentAttachmentDocument> _attachments =
        database.GetCollection<IntentAttachmentDocument>(MongoCollectionNames.IntentAttachments);

    private GridFSBucket Bucket => new(database, new GridFSBucketOptions { BucketName = GridFsBucketName });

    public async Task<int> CountByIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var filter = Builders<IntentAttachmentDocument>.Filter.Eq(x => x.IntentId, intentId.Value);
        var count = await _attachments.CountDocumentsAsync(filter, cancellationToken: ct).ConfigureAwait(false);
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    public async Task<IntentAttachment> AddAsync(
        IntentId intentId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);

        var fileId = ObjectId.GenerateNewId();
        var safeName = string.IsNullOrWhiteSpace(fileName) ? "upload" : Path.GetFileName(fileName);
        if (safeName.Length == 0)
        {
            safeName = "upload";
        }

        var declaredType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();

        var metadata = new BsonDocument
        {
            { "intent_id", intentId.Value },
            { "content_type", declaredType },
        };

        await Bucket
            .UploadFromStreamAsync(fileId, safeName, content, new GridFSUploadOptions { Metadata = metadata }, ct)
            .ConfigureAwait(false);

        var filter = Builders<GridFSFileInfo>.Filter.Eq(f => f.Id, fileId);
        using var cursor = await Bucket.FindAsync(filter, cancellationToken: ct).ConfigureAwait(false);
        var gfs = await cursor.FirstOrDefaultAsync(ct).ConfigureAwait(false);

        var length = gfs?.Length ?? 0L;
        var uploadedAt = gfs?.UploadDateTime ?? DateTime.UtcNow;

        var doc = new IntentAttachmentDocument
        {
            Id = fileId.ToString(),
            IntentId = intentId.Value,
            GridFsId = fileId.ToString(),
            FileName = safeName,
            ContentType = declaredType,
            SizeBytes = length,
            CreatedAt = DateTime.SpecifyKind(uploadedAt, DateTimeKind.Utc),
        };

        await _attachments.InsertOneAsync(doc, cancellationToken: ct).ConfigureAwait(false);

        return new IntentAttachment(
            doc.Id,
            doc.IntentId,
            doc.FileName,
            doc.ContentType,
            doc.SizeBytes,
            new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero));
    }

    public async Task DeleteAllForIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var filter = Builders<IntentAttachmentDocument>.Filter.Eq(x => x.IntentId, intentId.Value);
        var list = await _attachments.Find(filter).ToListAsync(ct).ConfigureAwait(false);
        foreach (var doc in list)
        {
            if (!ObjectId.TryParse(doc.GridFsId, out var oid))
            {
                continue;
            }

            try
            {
                await Bucket.DeleteAsync(oid, ct).ConfigureAwait(false);
            }
            catch (GridFSFileNotFoundException)
            {
                // already removed or inconsistent; continue cleanup
            }
        }

        await _attachments.DeleteManyAsync(filter, ct).ConfigureAwait(false);
    }
}

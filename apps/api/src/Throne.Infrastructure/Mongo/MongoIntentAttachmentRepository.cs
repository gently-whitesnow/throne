using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentAttachmentRepository(
    IMongoDatabase database) : IIntentAttachmentRepository
{
    private const string GridFsBucketName = "intent_attachment_fs";
    private const string CompressionStatePending = "pending";
    private const string CompressionStateReady = "ready";

    private readonly IMongoCollection<IntentAttachmentDocument> _attachments =
        database.GetCollection<IntentAttachmentDocument>(MongoCollectionNames.IntentAttachments);

    private GridFSBucket Bucket => new(database, new GridFSBucketOptions { BucketName = GridFsBucketName });

    private static FilterDefinition<IntentAttachmentDocument> ByIntent(IntentId intentId) =>
        Builders<IntentAttachmentDocument>.Filter.Eq(x => x.IntentId, intentId.Value);

    public async Task<int> CountByIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var filter = ByIntent(intentId);
        var count = await _attachments.CountDocumentsAsync(filter, cancellationToken: ct);
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    public async Task<IReadOnlyList<IntentAttachment>> ListByIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var filter = ByIntent(intentId);
        var docs = await _attachments
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(ct)
            ;

        return docs.Select(ToDomain).ToArray();
    }

    public async Task<UploadIntentAttachmentOutcome> AddAsync(
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
            ;

        var filter = Builders<GridFSFileInfo>.Filter.Eq(f => f.Id, fileId);
        using var cursor = await Bucket.FindAsync(filter, cancellationToken: ct);
        var gfs = await cursor.FirstOrDefaultAsync(ct);

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
            CompressionState = declaredType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? CompressionStatePending
                : null,
        };

        await _attachments.InsertOneAsync(doc, cancellationToken: ct);

        return new UploadIntentAttachmentOutcome(ToDomain(doc));
    }

    public async Task<IntentAttachmentContent?> OpenContentAsync(IntentId intentId, string attachmentId, CancellationToken ct)
    {
        var doc = await FindByIntentAndAttachmentAsync(intentId, attachmentId, ct);
        if (doc is null || !ObjectId.TryParse(doc.GridFsId, out var oid))
        {
            return null;
        }

        try
        {
            var stream = await Bucket.OpenDownloadStreamAsync(oid, cancellationToken: ct);
            return new IntentAttachmentContent(ToDomain(doc), stream);
        }
        catch (GridFSFileNotFoundException)
        {
            return null;
        }
    }

    public async Task<DeleteIntentAttachmentOutcome> DeleteAsync(IntentId intentId, string attachmentId, CancellationToken ct)
    {
        var doc = await FindByIntentAndAttachmentAsync(intentId, attachmentId, ct);
        if (doc is null)
        {
            return new DeleteIntentAttachmentOutcome.NotFound();
        }

        if (ObjectId.TryParse(doc.GridFsId, out var oid))
        {
            try
            {
                await Bucket.DeleteAsync(oid, ct);
            }
            catch (GridFSFileNotFoundException)
            {
                // Keep deleting metadata if the blob was already removed.
            }
        }

        var filter = Builders<IntentAttachmentDocument>.Filter.And(
            ByIntent(intentId),
            Builders<IntentAttachmentDocument>.Filter.Eq(x => x.Id, attachmentId));
        var result = await _attachments.DeleteOneAsync(filter, ct);
        if (result.DeletedCount == 0)
        {
            return new DeleteIntentAttachmentOutcome.NotFound();
        }

        return new DeleteIntentAttachmentOutcome.Deleted(intentId.Value, attachmentId);
    }

    public async Task DeleteAllForIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var filter = ByIntent(intentId);
        var list = await _attachments.Find(filter).ToListAsync(ct);
        foreach (var doc in list)
        {
            if (!ObjectId.TryParse(doc.GridFsId, out var oid))
            {
                continue;
            }

            try
            {
                await Bucket.DeleteAsync(oid, ct);
            }
            catch (GridFSFileNotFoundException)
            {
                // already removed or inconsistent; continue cleanup
            }
        }

        await _attachments.DeleteManyAsync(filter, ct);
    }

    private async Task<IntentAttachmentDocument?> FindByIntentAndAttachmentAsync(
        IntentId intentId,
        string attachmentId,
        CancellationToken ct)
    {
        var filter = Builders<IntentAttachmentDocument>.Filter.And(
            ByIntent(intentId),
            Builders<IntentAttachmentDocument>.Filter.Eq(x => x.Id, attachmentId));
        return await _attachments.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<PendingCompressionItem>> ListPendingCompressionAsync(int batchSize, CancellationToken ct)
    {
        if (batchSize < 1)
        {
            return [];
        }

        // Backfill-friendly: ловим как новые pending, так и старые image-аттачи без compression_state
        // (загруженные до появления флага). Главный признак — content_type начинается с "image/"
        // и compression_state != "ready".
        var filterBuilder = Builders<IntentAttachmentDocument>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.Regex(x => x.ContentType, new BsonRegularExpression("^image/", "i")),
            filterBuilder.Ne(x => x.CompressionState, CompressionStateReady));
        var docs = await _attachments
            .Find(filter)
            .SortBy(x => x.CreatedAt)
            .Limit(batchSize)
            .ToListAsync(ct)
            ;

        return docs
            .Select(d => new PendingCompressionItem(d.Id, d.GridFsId, d.ContentType))
            .ToArray();
    }

    public async Task<Stream?> OpenRawContentAsync(string gridFsId, CancellationToken ct)
    {
        if (!ObjectId.TryParse(gridFsId, out var oid))
        {
            return null;
        }

        try
        {
            return await Bucket.OpenDownloadStreamAsync(oid, cancellationToken: ct);
        }
        catch (GridFSFileNotFoundException)
        {
            return null;
        }
    }

    public async Task ApplyCompressionAsync(
        string attachmentId,
        string previousGridFsId,
        DownscaledImage compressed,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(compressed);
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(previousGridFsId);

        var newFileId = ObjectId.GenerateNewId();
        var existing = await _attachments
            .Find(Builders<IntentAttachmentDocument>.Filter.Eq(x => x.Id, attachmentId))
            .FirstOrDefaultAsync(ct)
            ;

        if (existing is null
            || string.Equals(existing.CompressionState, CompressionStateReady, StringComparison.Ordinal))
        {
            return;
        }

        var metadata = new BsonDocument
        {
            { "intent_id", existing.IntentId },
            { "content_type", compressed.MimeType },
            { "kind", "compressed" },
        };

        await using (var ms = new MemoryStream(compressed.Data, writable: false))
        {
            await Bucket
                .UploadFromStreamAsync(newFileId, existing.FileName, ms, new GridFSUploadOptions { Metadata = metadata }, ct)
                ;
        }

        var update = Builders<IntentAttachmentDocument>.Update
            .Set(x => x.GridFsId, newFileId.ToString())
            .Set(x => x.ContentType, compressed.MimeType)
            .Set(x => x.SizeBytes, compressed.Data.LongLength)
            .Set(x => x.DerivedWidth, compressed.Width)
            .Set(x => x.DerivedHeight, compressed.Height)
            .Set(x => x.CompressionState, CompressionStateReady);

        var updateFilter = Builders<IntentAttachmentDocument>.Filter.And(
            Builders<IntentAttachmentDocument>.Filter.Eq(x => x.Id, attachmentId),
            Builders<IntentAttachmentDocument>.Filter.Ne(x => x.CompressionState, CompressionStateReady));

        var updateResult = await _attachments
            .UpdateOneAsync(updateFilter, update, cancellationToken: ct)
            ;

        if (updateResult.ModifiedCount == 0)
        {
            // Lost the race (concurrent worker / delete): drop the freshly uploaded blob.
            try
            {
                await Bucket.DeleteAsync(newFileId, ct);
            }
            catch (GridFSFileNotFoundException)
            {
                // already gone
            }
            return;
        }

        if (ObjectId.TryParse(previousGridFsId, out var oldOid))
        {
            try
            {
                await Bucket.DeleteAsync(oldOid, ct);
            }
            catch (GridFSFileNotFoundException)
            {
                // original blob was already cleaned up
            }
        }
    }

    private static IntentAttachment ToDomain(IntentAttachmentDocument doc)
    {
        var isReady = string.Equals(doc.CompressionState, CompressionStateReady, StringComparison.Ordinal);
        return new IntentAttachment(
            doc.Id,
            doc.IntentId,
            doc.FileName,
            doc.ContentType,
            doc.SizeBytes,
            new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero),
            isReady,
            isReady ? doc.DerivedWidth : null,
            isReady ? doc.DerivedHeight : null);
    }
}

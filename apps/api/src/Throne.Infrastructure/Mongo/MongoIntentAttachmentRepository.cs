using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Throne.Application.Auth;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentAttachmentRepository(
    IMongoDatabase database,
    ICurrentUserAccessor currentUser) : IIntentAttachmentRepository
{
    private const string GridFsBucketName = "intent_attachment_fs";

    private readonly IMongoCollection<IntentAttachmentDocument> _attachments =
        database.GetCollection<IntentAttachmentDocument>(MongoCollectionNames.IntentAttachments);

    private GridFSBucket Bucket => new(database, new GridFSBucketOptions { BucketName = GridFsBucketName });

    private FilterDefinition<IntentAttachmentDocument> ByIntentAndOwner(IntentId intentId) =>
        Builders<IntentAttachmentDocument>.Filter.And(
            Builders<IntentAttachmentDocument>.Filter.Eq(x => x.IntentId, intentId.Value),
            Builders<IntentAttachmentDocument>.Filter.Eq(x => x.OwnerUserId, currentUser.UserId));

    public async Task<int> CountByIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var filter = ByIntentAndOwner(intentId);
        var count = await _attachments.CountDocumentsAsync(filter, cancellationToken: ct).ConfigureAwait(false);
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    public async Task<IReadOnlyList<IntentAttachment>> ListByIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var filter = ByIntentAndOwner(intentId);
        var docs = await _attachments
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

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

        var ownerUserId = currentUser.UserId;
        var metadata = new BsonDocument
        {
            { "intent_id", intentId.Value },
            { "owner_user_id", ownerUserId },
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
            OwnerUserId = ownerUserId,
            IntentId = intentId.Value,
            GridFsId = fileId.ToString(),
            FileName = safeName,
            ContentType = declaredType,
            SizeBytes = length,
            CreatedAt = DateTime.SpecifyKind(uploadedAt, DateTimeKind.Utc),
        };

        await _attachments.InsertOneAsync(doc, cancellationToken: ct).ConfigureAwait(false);

        return new UploadIntentAttachmentOutcome(ToDomain(doc));
    }

    public async Task<IntentAttachmentContent?> OpenContentAsync(IntentId intentId, string attachmentId, CancellationToken ct)
    {
        var doc = await FindByIntentAndAttachmentAsync(intentId, attachmentId, ct).ConfigureAwait(false);
        if (doc is null || !ObjectId.TryParse(doc.GridFsId, out var oid))
        {
            return null;
        }

        try
        {
            var stream = await Bucket.OpenDownloadStreamAsync(oid, cancellationToken: ct).ConfigureAwait(false);
            return new IntentAttachmentContent(ToDomain(doc), stream);
        }
        catch (GridFSFileNotFoundException)
        {
            return null;
        }
    }

    public async Task<DeleteIntentAttachmentOutcome> DeleteAsync(IntentId intentId, string attachmentId, CancellationToken ct)
    {
        var doc = await FindByIntentAndAttachmentAsync(intentId, attachmentId, ct).ConfigureAwait(false);
        if (doc is null)
        {
            return new DeleteIntentAttachmentOutcome.NotFound();
        }

        if (ObjectId.TryParse(doc.GridFsId, out var oid))
        {
            try
            {
                await Bucket.DeleteAsync(oid, ct).ConfigureAwait(false);
            }
            catch (GridFSFileNotFoundException)
            {
                // Keep deleting metadata if the blob was already removed.
            }
        }

        var filter = Builders<IntentAttachmentDocument>.Filter.And(
            ByIntentAndOwner(intentId),
            Builders<IntentAttachmentDocument>.Filter.Eq(x => x.Id, attachmentId));
        var result = await _attachments.DeleteOneAsync(filter, ct).ConfigureAwait(false);
        if (result.DeletedCount == 0)
        {
            return new DeleteIntentAttachmentOutcome.NotFound();
        }

        return new DeleteIntentAttachmentOutcome.Deleted(intentId.Value, attachmentId);
    }

    public async Task DeleteAllForIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var filter = ByIntentAndOwner(intentId);
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

    private async Task<IntentAttachmentDocument?> FindByIntentAndAttachmentAsync(
        IntentId intentId,
        string attachmentId,
        CancellationToken ct)
    {
        var filter = Builders<IntentAttachmentDocument>.Filter.And(
            ByIntentAndOwner(intentId),
            Builders<IntentAttachmentDocument>.Filter.Eq(x => x.Id, attachmentId));
        return await _attachments.Find(filter).FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    private static IntentAttachment ToDomain(IntentAttachmentDocument doc) =>
        new(
            doc.Id,
            string.IsNullOrWhiteSpace(doc.OwnerUserId) ? CurrentUserIds.LocalDev : doc.OwnerUserId,
            doc.IntentId,
            doc.FileName,
            doc.ContentType,
            doc.SizeBytes,
            new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero));
}

using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Throne.Application.Auth;
using Throne.Application.ChatUploads;
using Throne.Application.Ports;
using Throne.Infrastructure.ChatUploads;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoChatUploadRepository(
    IMongoDatabase database,
    ICurrentUserAccessor currentUser,
    IOptions<ChatUploadStorageOptions> storageOptions,
    TimeProvider clock) : IChatUploadRepository
{
    private readonly IMongoCollection<ChatUploadDocument> _uploads =
        database.GetCollection<ChatUploadDocument>(MongoCollectionNames.ChatUploads);

    private readonly ChatUploadStorageOptions _storage = storageOptions.Value;

    private FilterDefinition<ChatUploadDocument> ByOwner() =>
        Builders<ChatUploadDocument>.Filter.Eq(x => x.OwnerUserId, currentUser.UserId);

    public async Task<IReadOnlyList<ChatUpload>> ListAsync(CancellationToken ct)
    {
        var docs = await _uploads
            .Find(ByOwner())
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return docs.Select(ToDomain).ToArray();
    }

    public async Task<CreateChatUploadOutcome> AddAsync(
        ChatUploadManifest manifest,
        Stream archiveContent,
        long archiveSize,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(archiveContent);

        EnsureDirectoryExists(_storage.StoragePath);

        var id = Guid.NewGuid().ToString("N");
        var ownerUserId = currentUser.UserId;
        var fileName = $"{id}.zip";
        var fullPath = Path.Combine(_storage.StoragePath, fileName);

        try
        {
            if (archiveContent.CanSeek)
            {
                archiveContent.Position = 0;
            }

            await using var fs = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            await archiveContent.CopyToAsync(fs, ct);
        }
        catch (Exception)
        {
            TryDeleteFile(fullPath);
            throw;
        }

        var createdAt = clock.GetUtcNow();
        var doc = new ChatUploadDocument
        {
            Id = id,
            OwnerUserId = ownerUserId,
            Agent = manifest.Agent,
            AgentVersion = manifest.AgentVersion,
            Device = manifest.Device,
            DeviceDisplayName = manifest.DeviceDisplayName,
            DateRangeFrom = manifest.DateRange.From.UtcDateTime,
            DateRangeTo = manifest.DateRange.To.UtcDateTime,
            ConversationCount = manifest.Conversations.Count,
            SizeBytes = archiveSize,
            FilePath = fullPath,
            Status = ChatUploadStatusNames.Uploaded,
            CreatedAt = createdAt.UtcDateTime,
        };

        try
        {
            await _uploads.InsertOneAsync(doc, cancellationToken: ct);
        }
        catch (Exception)
        {
            TryDeleteFile(fullPath);
            throw;
        }

        return new CreateChatUploadOutcome(ToDomain(doc));
    }

    public async Task<ChatUploadContent?> OpenContentAsync(string id, CancellationToken ct)
    {
        var doc = await FindByIdAsync(id, ct);
        if (doc is null)
        {
            return null;
        }

        if (!File.Exists(doc.FilePath))
        {
            return null;
        }

        var stream = new FileStream(
            doc.FilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        return new ChatUploadContent(ToDomain(doc), stream);
    }

    public async Task<DeleteChatUploadOutcome> DeleteAsync(string id, CancellationToken ct)
    {
        var doc = await FindByIdAsync(id, ct);
        if (doc is null)
        {
            return new DeleteChatUploadOutcome.NotFound();
        }

        TryDeleteFile(doc.FilePath);

        var result = await _uploads.DeleteOneAsync(
            Builders<ChatUploadDocument>.Filter.And(
                ByOwner(),
                Builders<ChatUploadDocument>.Filter.Eq(x => x.Id, id)),
            ct);

        if (result.DeletedCount == 0)
        {
            return new DeleteChatUploadOutcome.NotFound();
        }

        return new DeleteChatUploadOutcome.Deleted(id);
    }

    private async Task<ChatUploadDocument?> FindByIdAsync(string id, CancellationToken ct)
    {
        var filter = Builders<ChatUploadDocument>.Filter.And(
            ByOwner(),
            Builders<ChatUploadDocument>.Filter.Eq(x => x.Id, id));
        return await _uploads.Find(filter).FirstOrDefaultAsync(ct);
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "ChatUploads:StoragePath is not configured. Set the option or mount a host volume directory.");
        }

        Directory.CreateDirectory(path);
    }

    private static void TryDeleteFile(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
            // best-effort cleanup; surfaced via metrics in the future
        }
        catch (UnauthorizedAccessException)
        {
            // best-effort cleanup
        }
    }

    private static ChatUpload ToDomain(ChatUploadDocument doc) => new(
        Id: doc.Id,
        OwnerUserId: string.IsNullOrWhiteSpace(doc.OwnerUserId) ? CurrentUserIds.LocalDev : doc.OwnerUserId,
        Agent: doc.Agent,
        AgentVersion: doc.AgentVersion,
        Device: doc.Device,
        DeviceDisplayName: doc.DeviceDisplayName,
        DateRangeFrom: new DateTimeOffset(DateTime.SpecifyKind(doc.DateRangeFrom, DateTimeKind.Utc), TimeSpan.Zero),
        DateRangeTo: new DateTimeOffset(DateTime.SpecifyKind(doc.DateRangeTo, DateTimeKind.Utc), TimeSpan.Zero),
        ConversationCount: doc.ConversationCount,
        SizeBytes: doc.SizeBytes,
        Status: string.IsNullOrWhiteSpace(doc.Status) ? ChatUploadStatusNames.Uploaded : doc.Status,
        CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero));
}

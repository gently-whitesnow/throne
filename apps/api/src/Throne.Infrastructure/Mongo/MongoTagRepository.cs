using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Infrastructure.Mongo.Documents;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoTagRepository(IMongoDatabase database, MongoSessionAccessor sessions) : ITagRepository
{
    private const int DuplicateKeyCode = 11000;

    private readonly IMongoCollection<TagDocument> _tags =
        database.GetCollection<TagDocument>(MongoCollectionNames.Tags);

    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    public async Task<IReadOnlyList<Tag>> ListAllAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var docs = session is null
            ? await _tags.Find(FilterDefinition<TagDocument>.Empty).SortBy(d => d.Name).ToListAsync(ct)
            : await _tags.Find(session, FilterDefinition<TagDocument>.Empty).SortBy(d => d.Name).ToListAsync(ct);

        var result = new List<Tag>(docs.Count);
        foreach (var doc in docs)
        {
            result.Add(MapToDomain(doc));
        }
        return result;
    }

    public async Task<Tag?> GetByIdAsync(TagId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var doc = session is null
            ? await _tags.Find(d => d.Id == id.Value).FirstOrDefaultAsync(ct)
            : await _tags.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
        return doc is null ? null : MapToDomain(doc);
    }

    public async Task<Tag?> FindByNameAsync(string normalizedName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(normalizedName);
        var session = sessions.Current;
        var doc = session is null
            ? await _tags.Find(d => d.Name == normalizedName).FirstOrDefaultAsync(ct)
            : await _tags.Find(session, d => d.Name == normalizedName).FirstOrDefaultAsync(ct);
        return doc is null ? null : MapToDomain(doc);
    }

    public async Task<EnsureTagOutcome> EnsureByNameAsync(string normalizedName, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(normalizedName);
        var session = sessions.Current;

        var existing = await FindByNameAsync(normalizedName, ct);
        if (existing is not null)
        {
            return new EnsureTagOutcome.Existed(existing);
        }

        var tag = Tag.Create(TagId.New(), normalizedName, now);
        var doc = MapToDocument(tag);

        try
        {
            if (session is null)
            {
                await _tags.InsertOneAsync(doc, options: null, ct);
            }
            else
            {
                await _tags.InsertOneAsync(session, doc, options: null, ct);
            }
            return new EnsureTagOutcome.Created(tag);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == DuplicateKeyCode)
        {
            // Race: another process inserted the same name between FindByNameAsync and InsertOneAsync.
            var raced = await FindByNameAsync(normalizedName, ct)
                ?? throw new InvalidOperationException(
                    $"Tag '{normalizedName}' duplicate-key error but FindByNameAsync returned null.");
            return new EnsureTagOutcome.Existed(raced);
        }
    }

    public async Task<CreateTagOutcome> CreateAsync(string rawName, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawName);
        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoTagRepository.CreateAsync must run inside IUnitOfWork.ExecuteAsync.");

        var normalized = TagNames.Normalize(rawName);
        var existing = await FindByNameAsync(normalized, ct);
        if (existing is not null)
        {
            return new CreateTagOutcome.NameTaken(existing);
        }

        var tag = Tag.Create(TagId.New(), normalized, now);
        try
        {
            await _tags.InsertOneAsync(session, MapToDocument(tag), options: null, ct);
            return new CreateTagOutcome.Created(tag);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == DuplicateKeyCode)
        {
            var raced = await FindByNameAsync(normalized, ct)
                ?? throw new InvalidOperationException(
                    $"Tag '{normalized}' duplicate-key error but FindByNameAsync returned null.");
            return new CreateTagOutcome.NameTaken(raced);
        }
    }

    public async Task<RenameTagOutcome> RenameAsync(
        TagId id,
        int expectedVersion,
        string rawName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoTagRepository.RenameAsync must run inside IUnitOfWork.ExecuteAsync.");

        var doc = await _tags.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
        if (doc is null)
        {
            return new RenameTagOutcome.NotFound();
        }

        if (doc.CurrentVersion != expectedVersion)
        {
            return new RenameTagOutcome.VersionConflict(doc.CurrentVersion);
        }

        var tag = MapToDomain(doc);
        var changed = tag.Rename(rawName, now);
        if (!changed)
        {
            return new RenameTagOutcome.NoChange(tag);
        }

        var collision = await _tags
            .Find(session, d => d.Name == tag.Name && d.Id != id.Value)
            .FirstOrDefaultAsync(ct);
        if (collision is not null)
        {
            return new RenameTagOutcome.NameTaken(MapToDomain(collision));
        }

        var update = Builders<TagDocument>.Update
            .Set(d => d.Name, tag.Name)
            .Set(d => d.CurrentVersion, tag.CurrentVersion)
            .Set(d => d.UpdatedAt, tag.UpdatedAt.UtcDateTime);

        try
        {
            var updateResult = await _tags.UpdateOneAsync(
                session,
                d => d.Id == id.Value && d.CurrentVersion == expectedVersion,
                update,
                options: null,
                ct);

            if (updateResult.ModifiedCount == 0)
            {
                var fresh = await _tags.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
                return fresh is null
                    ? new RenameTagOutcome.NotFound()
                    : new RenameTagOutcome.VersionConflict(fresh.CurrentVersion);
            }
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == DuplicateKeyCode)
        {
            var raced = await _tags.Find(session, d => d.Name == tag.Name && d.Id != id.Value)
                .FirstOrDefaultAsync(ct);
            if (raced is not null)
            {
                return new RenameTagOutcome.NameTaken(MapToDomain(raced));
            }
            throw;
        }

        return new RenameTagOutcome.Renamed(tag);
    }

    public async Task<TagUsage> GetUsageAsync(TagId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var filter = Builders<IntentDocument>.Filter.AnyEq(d => d.TagIds, id.Value);
        var count = session is null
            ? await _intents.CountDocumentsAsync(filter, options: null, ct)
            : await _intents.CountDocumentsAsync(session, filter, options: null, ct);
        return new TagUsage((int)count);
    }

    public async Task<DeleteTagOutcome> DeleteAsync(TagId id, DateTimeOffset now, CancellationToken ct)
    {
        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoTagRepository.DeleteAsync must run inside IUnitOfWork.ExecuteAsync.");

        var doc = await _tags.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
        if (doc is null)
        {
            return new DeleteTagOutcome.NotFound();
        }

        var nowUtc = now.UtcDateTime;
        var detachUpdate = Builders<IntentDocument>.Update
            .Pull(d => d.TagIds, id.Value)
            .Set(d => d.UpdatedAt, nowUtc);

        var attachedFilter = Builders<IntentDocument>.Filter.AnyEq(d => d.TagIds, id.Value);
        var affected = await _intents.Find(session, attachedFilter).ToListAsync(ct);

        if (affected.Count > 0)
        {
            await _intents.UpdateManyAsync(session, attachedFilter, detachUpdate, options: null, ct);
        }

        var deleteResult = await _tags.DeleteOneAsync(session, d => d.Id == id.Value, options: null, ct);
        if (deleteResult.DeletedCount == 0)
        {
            return new DeleteTagOutcome.NotFound();
        }

        var refreshed = new List<Intent>(affected.Count);
        foreach (var oldDoc in affected)
        {
            var fresh = await _intents.Find(session, d => d.Id == oldDoc.Id).FirstOrDefaultAsync(ct);
            if (fresh is not null)
            {
                refreshed.Add(MapIntentToDomain(fresh));
            }
        }

        return new DeleteTagOutcome.Deleted(id, refreshed);
    }

    private static TagDocument MapToDocument(Tag tag) => new()
    {
        Id = tag.Id.Value,
        Name = tag.Name,
        CurrentVersion = tag.CurrentVersion,
        CreatedAt = tag.CreatedAt.UtcDateTime,
        UpdatedAt = tag.UpdatedAt.UtcDateTime,
    };

    private static Tag MapToDomain(TagDocument doc) => Tag.Restore(
        id: new TagId(doc.Id),
        name: doc.Name,
        currentVersion: doc.CurrentVersion,
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc));

    private static Intent MapIntentToDomain(IntentDocument doc) => Intent.Restore(
        id: new IntentId(doc.Id),
        text: doc.Text,
        status: string.IsNullOrWhiteSpace(doc.Status) ? Throne.Domain.Intents.IntentStatusNames.Draft : doc.Status,
        currentVersion: doc.CurrentVersion,
        tagIds: doc.TagIds.Select(v => new TagId(v)).ToList(),
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc));
}

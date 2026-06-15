using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;
using Throne.Infrastructure.Mongo.Documents;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoTagRepository : MongoRepositoryBase<TagDocument, string>, ITagRepository
{
    // Secondary collection (intent fan-out for tag-detach + usage count) lives here
    // as a plain field — only the primary `tags` collection goes through the base.
    private readonly IMongoCollection<IntentDocument> _intents;

    public MongoTagRepository(IMongoDatabase database, MongoSessionAccessor sessions)
        : base(database, MongoCollectionNames.Tags, sessions)
    {
        _intents = database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);
    }

    protected override FilterDefinition<TagDocument> ById(string id) =>
        Builders<TagDocument>.Filter.Eq(d => d.Id, id);

    public async Task<IReadOnlyList<Tag>> ListAllAsync(CancellationToken ct)
    {
        var docs = await Find(FilterDefinition<TagDocument>.Empty).SortBy(d => d.Name).ToListAsync(ct);
        var result = new List<Tag>(docs.Count);
        foreach (var doc in docs)
        {
            result.Add(MongoTagMapper.ToDomain(doc));
        }
        return result;
    }

    public async Task<Tag?> GetByIdAsync(TagId id, CancellationToken ct)
    {
        var doc = await FindByIdAsync(id.Value, ct);
        return doc is null ? null : MongoTagMapper.ToDomain(doc);
    }

    public async Task<Tag?> FindByNameAsync(string normalizedName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(normalizedName);
        var doc = await FindOneAsync(Builders<TagDocument>.Filter.Eq(d => d.Name, normalizedName), ct);
        return doc is null ? null : MongoTagMapper.ToDomain(doc);
    }

    public async Task<EnsureTagOutcome> EnsureByNameAsync(string normalizedName, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(normalizedName);

        var existing = await FindByNameAsync(normalizedName, ct);
        if (existing is not null)
        {
            return new EnsureTagOutcome.Existed(existing);
        }

        var tag = Tag.Create(TagId.New(), normalizedName, now);
        var doc = MongoTagMapper.ToDocument(tag);

        try
        {
            await InsertOneAsync(doc, ct);
            return new EnsureTagOutcome.Created(tag);
        }
        catch (MongoWriteException ex) when (MongoWriteExceptionHelper.IsDuplicateKey(ex))
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
        _ = RequireSession(nameof(CreateAsync));

        var normalized = TagNames.Normalize(rawName);
        var existing = await FindByNameAsync(normalized, ct);
        if (existing is not null)
        {
            return new CreateTagOutcome.NameTaken(existing);
        }

        var tag = Tag.Create(TagId.New(), normalized, now);
        try
        {
            await InsertOneAsync(MongoTagMapper.ToDocument(tag), ct);
            return new CreateTagOutcome.Created(tag);
        }
        catch (MongoWriteException ex) when (MongoWriteExceptionHelper.IsDuplicateKey(ex))
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
        _ = RequireSession(nameof(RenameAsync));

        var doc = await FindByIdAsync(id.Value, ct);
        if (doc is null)
        {
            return new RenameTagOutcome.NotFound();
        }
        if (doc.CurrentVersion != expectedVersion)
        {
            return new RenameTagOutcome.VersionConflict(doc.CurrentVersion);
        }

        var tag = MongoTagMapper.ToDomain(doc);
        var changed = tag.Rename(rawName, now);
        if (!changed)
        {
            return new RenameTagOutcome.NoChange(tag);
        }

        var collisionFilter = Builders<TagDocument>.Filter.And(
            Builders<TagDocument>.Filter.Eq(d => d.Name, tag.Name),
            Builders<TagDocument>.Filter.Ne(d => d.Id, id.Value));
        var collision = await FindOneAsync(collisionFilter, ct);
        if (collision is not null)
        {
            return new RenameTagOutcome.NameTaken(MongoTagMapper.ToDomain(collision));
        }

        var update = Builders<TagDocument>.Update
            .Set(d => d.Name, tag.Name)
            .Set(d => d.CurrentVersion, tag.CurrentVersion)
            .Set(d => d.UpdatedAt, tag.UpdatedAt.UtcDateTime);
        var casFilter = Builders<TagDocument>.Filter.And(
            ById(id.Value),
            Builders<TagDocument>.Filter.Eq(d => d.CurrentVersion, expectedVersion));

        try
        {
            if (!await TryUpdateAsync(casFilter, update, ct))
            {
                var fresh = await FindByIdAsync(id.Value, ct);
                return fresh is null
                    ? new RenameTagOutcome.NotFound()
                    : new RenameTagOutcome.VersionConflict(fresh.CurrentVersion);
            }
        }
        catch (MongoWriteException ex) when (MongoWriteExceptionHelper.IsDuplicateKey(ex))
        {
            var raced = await FindOneAsync(collisionFilter, ct);
            if (raced is not null)
            {
                return new RenameTagOutcome.NameTaken(MongoTagMapper.ToDomain(raced));
            }
            throw;
        }

        return new RenameTagOutcome.Renamed(tag);
    }

    public async Task<SetTagDefaultRepositoriesOutcome> SetDefaultRepositoriesAsync(
        TagId id,
        int expectedVersion,
        IReadOnlyList<TagDefaultRepository> defaultRepositories,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(defaultRepositories);
        _ = RequireSession(nameof(SetDefaultRepositoriesAsync));

        var doc = await FindByIdAsync(id.Value, ct);
        if (doc is null)
        {
            return new SetTagDefaultRepositoriesOutcome.NotFound();
        }
        if (doc.CurrentVersion != expectedVersion)
        {
            return new SetTagDefaultRepositoriesOutcome.VersionConflict(doc.CurrentVersion);
        }

        var tag = MongoTagMapper.ToDomain(doc);
        var changed = tag.ReplaceDefaultRepositories(defaultRepositories, now);
        if (!changed)
        {
            return new SetTagDefaultRepositoriesOutcome.NoChange(tag);
        }

        var subdocuments = tag.DefaultRepositories.Count == 0
            ? []
            : tag.DefaultRepositories.Select(MongoTagMapper.DefaultRepositoryToDocument).ToList();
        var update = Builders<TagDocument>.Update
            .Set(d => d.DefaultRepositories, subdocuments)
            .Set(d => d.CurrentVersion, tag.CurrentVersion)
            .Set(d => d.UpdatedAt, tag.UpdatedAt.UtcDateTime);
        var casFilter = Builders<TagDocument>.Filter.And(
            ById(id.Value),
            Builders<TagDocument>.Filter.Eq(d => d.CurrentVersion, expectedVersion));

        if (!await TryUpdateAsync(casFilter, update, ct))
        {
            var fresh = await FindByIdAsync(id.Value, ct);
            return fresh is null
                ? new SetTagDefaultRepositoriesOutcome.NotFound()
                : new SetTagDefaultRepositoriesOutcome.VersionConflict(fresh.CurrentVersion);
        }

        return new SetTagDefaultRepositoriesOutcome.Updated(tag);
    }

    public async Task<TagUsage> GetUsageAsync(TagId id, CancellationToken ct)
    {
        var session = Sessions.Current;
        var filter = Builders<IntentDocument>.Filter.AnyEq(d => d.TagIds, id.Value);
        var count = session is null
            ? await _intents.CountDocumentsAsync(filter, options: null, ct)
            : await _intents.CountDocumentsAsync(session, filter, options: null, ct);
        return new TagUsage((int)count);
    }

    public async Task<DeleteTagOutcome> DeleteAsync(TagId id, DateTimeOffset now, CancellationToken ct)
    {
        var session = RequireSession(nameof(DeleteAsync));

        var doc = await FindByIdAsync(id.Value, ct);
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

        var deleteResult = await DeleteOneAsync(ById(id.Value), ct);
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
                refreshed.Add(MongoTagMapper.IntentToDomain(fresh));
            }
        }

        return new DeleteTagOutcome.Deleted(id, refreshed);
    }

    private IClientSessionHandle RequireSession(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"MongoTagRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");
}

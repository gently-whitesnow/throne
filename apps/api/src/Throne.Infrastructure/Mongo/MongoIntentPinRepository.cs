using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentPinRepository
    : MongoRepositoryBase<IntentPinDocument, string>, IIntentPinRepository
{
    // Pin is the primary aggregate; intents/tags are read-only sidecars used for
    // validation + ordering, so they stay as plain fields outside the base.
    private readonly IMongoCollection<IntentDocument> _intents;
    private readonly IMongoCollection<TagDocument> _tags;

    public MongoIntentPinRepository(IMongoDatabase database, MongoSessionAccessor sessions)
        : base(database, MongoCollectionNames.IntentPins, sessions)
    {
        _intents = database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);
        _tags = database.GetCollection<TagDocument>(MongoCollectionNames.Tags);
    }

    protected override FilterDefinition<IntentPinDocument> ById(string id) =>
        Builders<IntentPinDocument>.Filter.Eq(d => d.Id, id);

    public async Task<PinIntentOutcome> PinAsync(
        IntentId intentId,
        TagId contextTagId,
        IntentId? beforeId,
        IntentId? afterId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var session = RequireSession(nameof(PinAsync));

        var intent = await LoadIntentAsync(intentId, session, ct);
        if (intent is null)
        {
            return new PinIntentOutcome.IntentNotFound();
        }

        var tagExists = await _tags.Find(session, Builders<TagDocument>.Filter.Eq(d => d.Id, contextTagId.Value))
            .AnyAsync(ct);
        if (!tagExists)
        {
            return new PinIntentOutcome.ContextTagNotFound(contextTagId.Value);
        }

        var (beforeKey, afterKey, pivotMissing) = await ResolvePivotKeysAsync(session, contextTagId, beforeId, afterId, ct);
        if (pivotMissing is not null)
        {
            return new PinIntentOutcome.PivotNotFound(pivotMissing);
        }

        var existing = await FindExistingPinAsync(session, intentId, contextTagId, ct);
        var reorderRequested = beforeId is not null || afterId is not null;

        if (existing is not null && !reorderRequested)
        {
            // Idempotent re-pin: same context, no pivots ⇒ keep current position.
            var domainPin = ToDomain(existing);
            return new PinIntentOutcome.Pinned(MapToIntent(intent), domainPin, Created: false);
        }

        string newKey;
        if (reorderRequested)
        {
            newKey = FractionalIndex.Between(beforeKey, afterKey);
        }
        else
        {
            // Append to the tail: pick the current max pin_sort_key for the context as the
            // left pivot and let FractionalIndex generate a strictly-greater key.
            var maxKey = await GetTailKeyAsync(session, contextTagId, ct);
            newKey = FractionalIndex.Between(maxKey, after: null);
        }

        if (existing is not null)
        {
            existing.PinSortKey = newKey;
            await TryUpdateAsync(
                ById(existing.Id),
                Builders<IntentPinDocument>.Update.Set(d => d.PinSortKey, newKey),
                ct);
            var pin = new IntentPin(intentId, contextTagId, newKey, ToUtc(existing.CreatedAt));
            return new PinIntentOutcome.Pinned(MapToIntent(intent), pin, Created: false);
        }

        var doc = new IntentPinDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            IntentId = intentId.Value,
            ContextTagId = contextTagId.Value,
            PinSortKey = newKey,
            CreatedAt = now.UtcDateTime,
        };
        await InsertOneAsync(doc, ct);
        var created = new IntentPin(intentId, contextTagId, newKey, now);
        return new PinIntentOutcome.Pinned(MapToIntent(intent), created, Created: true);
    }

    public async Task<UnpinIntentOutcome> UnpinAsync(
        IntentId intentId,
        TagId contextTagId,
        CancellationToken ct)
    {
        var session = RequireSession(nameof(UnpinAsync));

        var intent = await LoadIntentAsync(intentId, session, ct);
        if (intent is null)
        {
            return new UnpinIntentOutcome.IntentNotFound();
        }

        var result = await DeleteOneAsync(
            Builders<IntentPinDocument>.Filter.And(
                Builders<IntentPinDocument>.Filter.Eq(d => d.IntentId, intentId.Value),
                Builders<IntentPinDocument>.Filter.Eq(d => d.ContextTagId, contextTagId.Value)),
            ct);

        return new UnpinIntentOutcome.Unpinned(MapToIntent(intent), contextTagId.Value, Removed: result.DeletedCount > 0);
    }

    public async Task<MovePinOutcome> MoveAsync(
        IntentId intentId,
        TagId contextTagId,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct)
    {
        if (beforeId is null && afterId is null)
        {
            throw new ArgumentException("At least one of beforeId / afterId must be supplied.", nameof(beforeId));
        }

        var session = RequireSession(nameof(MoveAsync));

        var intent = await LoadIntentAsync(intentId, session, ct);
        if (intent is null)
        {
            return new MovePinOutcome.IntentNotFound();
        }

        var existing = await FindExistingPinAsync(session, intentId, contextTagId, ct);
        if (existing is null)
        {
            return new MovePinOutcome.PinNotFound();
        }

        var (beforeKey, afterKey, pivotMissing) = await ResolvePivotKeysAsync(session, contextTagId, beforeId, afterId, ct);
        if (pivotMissing is not null)
        {
            return new MovePinOutcome.PivotNotFound(pivotMissing);
        }

        var newKey = FractionalIndex.Between(beforeKey, afterKey);
        if (string.Equals(existing.PinSortKey, newKey, StringComparison.Ordinal))
        {
            var stable = new IntentPin(intentId, contextTagId, newKey, ToUtc(existing.CreatedAt));
            return new MovePinOutcome.Moved(MapToIntent(intent), stable, Changed: false);
        }

        existing.PinSortKey = newKey;
        await TryUpdateAsync(
            ById(existing.Id),
            Builders<IntentPinDocument>.Update.Set(d => d.PinSortKey, newKey),
            ct);

        var pin = new IntentPin(intentId, contextTagId, newKey, ToUtc(existing.CreatedAt));
        return new MovePinOutcome.Moved(MapToIntent(intent), pin, Changed: true);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<IntentPin>>> GetPinnedInAsync(
        IReadOnlyList<string> intentIds,
        CancellationToken ct)
    {
        var result = new Dictionary<string, IReadOnlyList<IntentPin>>(StringComparer.Ordinal);
        if (intentIds is null || intentIds.Count == 0)
        {
            return result;
        }

        var filter = Builders<IntentPinDocument>.Filter.In(d => d.IntentId, intentIds);
        var docs = await Find(filter).ToListAsync(ct);

        foreach (var doc in docs)
        {
            if (!result.TryGetValue(doc.IntentId, out var list))
            {
                list = new List<IntentPin>();
                result[doc.IntentId] = list;
            }
            ((List<IntentPin>)list).Add(ToDomain(doc));
        }
        return result;
    }

    public async Task<IReadOnlyList<IntentPin>> ListAllAsync(CancellationToken ct)
    {
        var allFilter = Builders<IntentPinDocument>.Filter.Empty;
        var docs = await Find(allFilter)
            .Sort(Builders<IntentPinDocument>.Sort
                .Ascending(d => d.ContextTagId)
                .Ascending(d => d.PinSortKey))
            .ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    private async Task<IntentDocument?> LoadIntentAsync(IntentId intentId, IClientSessionHandle session, CancellationToken ct)
    {
        var filter = Builders<IntentDocument>.Filter.Eq(d => d.Id, intentId.Value);
        return await _intents.Find(session, filter).FirstOrDefaultAsync(ct);
    }

    private Task<IntentPinDocument?> FindExistingPinAsync(
        IClientSessionHandle session,
        IntentId intentId,
        TagId contextTagId,
        CancellationToken ct)
    {
        // session parameter retained so callers visually thread the transaction through the
        // resolver chain; the base helper auto-routes via Sessions.Current.
        _ = session;
        var filter = Builders<IntentPinDocument>.Filter.And(
            Builders<IntentPinDocument>.Filter.Eq(d => d.IntentId, intentId.Value),
            Builders<IntentPinDocument>.Filter.Eq(d => d.ContextTagId, contextTagId.Value));
        return FindOneAsync(filter, ct);
    }

    private async Task<(string? BeforeKey, string? AfterKey, string? Missing)> ResolvePivotKeysAsync(
        IClientSessionHandle session,
        TagId contextTagId,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct)
    {
        if (beforeId is null && afterId is null)
        {
            return (null, null, null);
        }

        var ids = new List<string>(2);
        if (beforeId is not null)
        {
            ids.Add(beforeId.Value.Value);
        }
        if (afterId is not null)
        {
            ids.Add(afterId.Value.Value);
        }

        var filter = Builders<IntentPinDocument>.Filter.And(
            Builders<IntentPinDocument>.Filter.Eq(d => d.ContextTagId, contextTagId.Value),
            Builders<IntentPinDocument>.Filter.In(d => d.IntentId, ids));
        _ = session;
        var docs = await Find(filter)
            .Project(d => new { d.IntentId, d.PinSortKey })
            .ToListAsync(ct);

        string? Lookup(IntentId? id) => id is null
            ? null
            : docs.FirstOrDefault(d => d.IntentId == id.Value.Value)?.PinSortKey;

        var beforeKey = Lookup(beforeId);
        var afterKey = Lookup(afterId);
        if (beforeId is not null && beforeKey is null)
        {
            return (null, null, beforeId.Value.Value);
        }
        if (afterId is not null && afterKey is null)
        {
            return (null, null, afterId.Value.Value);
        }
        return (beforeKey, afterKey, null);
    }

    private async Task<string?> GetTailKeyAsync(IClientSessionHandle session, TagId contextTagId, CancellationToken ct)
    {
        _ = session;
        var filter = Builders<IntentPinDocument>.Filter.Eq(d => d.ContextTagId, contextTagId.Value);
        var doc = await Find(filter)
            .Sort(Builders<IntentPinDocument>.Sort.Descending(d => d.PinSortKey))
            .Limit(1)
            .Project(d => d.PinSortKey)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrEmpty(doc) ? null : doc;
    }

    private IClientSessionHandle RequireSession(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"MongoIntentPinRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");

    private static IntentPin ToDomain(IntentPinDocument doc) => new IntentPin(
        new IntentId(doc.IntentId),
        new TagId(doc.ContextTagId),
        doc.PinSortKey,
        ToUtc(doc.CreatedAt));

    private static Intent MapToIntent(IntentDocument doc) => Intent.Restore(
        id: new IntentId(doc.Id),
        text: doc.Text,
        status: string.IsNullOrWhiteSpace(doc.Status) ? IntentStatusNames.Draft : doc.Status,
        currentVersion: doc.CurrentVersion,
        tagIds: doc.TagIds.Select(v => new TagId(v)).ToList(),
        sortKey: doc.SortKey,
        createdAt: ToUtc(doc.CreatedAt),
        updatedAt: ToUtc(doc.UpdatedAt));

    private static DateTimeOffset ToUtc(DateTime dt) =>
        new(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}

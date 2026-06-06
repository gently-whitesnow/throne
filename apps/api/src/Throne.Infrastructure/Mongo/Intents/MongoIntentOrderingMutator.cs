using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Intents;

internal sealed class MongoIntentOrderingMutator(
    IMongoDatabase database,
    MongoSessionAccessor sessions)
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    public async Task<MoveIntentOutcome> MoveBetweenAsync(
        IntentId id,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct)
    {
        if (beforeId is null && afterId is null)
        {
            throw new ArgumentException("At least one of beforeId / afterId must be supplied.", nameof(beforeId));
        }

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentOrderingMutator.MoveBetweenAsync must run inside IUnitOfWork.ExecuteAsync.");

        var (beforeKey, beforeMissing) = await ResolvePivotAsync(session, beforeId, ct);
        if (beforeMissing)
        {
            return new MoveIntentOutcome.PivotNotFound(beforeId!.Value.Value);
        }
        var (afterKey, afterMissing) = await ResolvePivotAsync(session, afterId, ct);
        if (afterMissing)
        {
            return new MoveIntentOutcome.PivotNotFound(afterId!.Value.Value);
        }

        var byId = IntentCollectionFilters.ById(id.Value);
        var document = await _intents.Find(session, byId).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new MoveIntentOutcome.NotFound();
        }

        var newSortKey = FractionalIndex.Between(beforeKey, afterKey);
        if (string.Equals(document.SortKey, newSortKey, StringComparison.Ordinal))
        {
            return new MoveIntentOutcome.Moved(IntentDocumentMapper.ToDomain(document), Changed: false);
        }

        // Reorder is purely positional: do not touch updated_at or current_version.
        var update = Builders<IntentDocument>.Update.Set(d => d.SortKey, newSortKey);
        var updateResult = await _intents.UpdateOneAsync(session, byId, update, options: null, ct);
        if (updateResult.ModifiedCount == 0)
        {
            var fresh = await _intents.Find(session, byId).FirstOrDefaultAsync(ct);
            return fresh is null
                ? new MoveIntentOutcome.NotFound()
                : new MoveIntentOutcome.Moved(IntentDocumentMapper.ToDomain(fresh), Changed: false);
        }

        document.SortKey = newSortKey;
        return new MoveIntentOutcome.Moved(IntentDocumentMapper.ToDomain(document), Changed: true);
    }

    private async Task<(string? Key, bool Missing)> ResolvePivotAsync(
        IClientSessionHandle session,
        IntentId? pivotId,
        CancellationToken ct)
    {
        if (pivotId is null)
        {
            return (null, false);
        }

        var pivotFilter = IntentCollectionFilters.ById(pivotId.Value.Value);
        var key = await _intents.Find(session, pivotFilter)
            .Project(d => d.SortKey)
            .FirstOrDefaultAsync(ct);
        return key is null ? (null, true) : (key, false);
    }
}

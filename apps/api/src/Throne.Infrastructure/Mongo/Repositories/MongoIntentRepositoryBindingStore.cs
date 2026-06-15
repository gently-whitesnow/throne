using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

/// <summary>
/// Mongo-backed <see cref="IIntentRepositoryBindingRepository"/>. All sessions are
/// resolved through <see cref="MongoSessionAccessor"/> so calls that run inside
/// <see cref="IUnitOfWork.ExecuteAsync"/> participate in the transaction; reads outside a
/// session are still safe (no implicit writes).
/// </summary>
internal sealed class MongoIntentRepositoryBindingStore
    : MongoRepositoryBase<IntentRepositoryBindingDocument, string>, IIntentRepositoryBindingRepository
{
    public MongoIntentRepositoryBindingStore(IMongoDatabase database, MongoSessionAccessor sessions)
        : base(database, MongoCollectionNames.IntentRepositoryBindings, sessions)
    {
    }

    protected override FilterDefinition<IntentRepositoryBindingDocument> ById(string id) =>
        Builders<IntentRepositoryBindingDocument>.Filter.Eq(d => d.Id, id);

    public async Task<CreateBindingOutcome> CreateAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);

        var existing = await FindByCoordinateAsync(binding.IntentId, binding.Coordinate, ct);
        if (existing is not null)
        {
            return new CreateBindingOutcome.Duplicate(IntentRepositoryBindingDocumentMapper.ToDomain(existing));
        }

        try
        {
            await InsertOneAsync(IntentRepositoryBindingDocumentMapper.ToDocument(binding), ct);
        }
        catch (MongoWriteException ex) when (MongoWriteExceptionHelper.IsDuplicateKey(ex))
        {
            // Lost the race against a concurrent insert. Re-read so the caller sees the
            // winning row instead of a misleading «server error» bubble.
            var loser = await FindByCoordinateAsync(binding.IntentId, binding.Coordinate, ct);
            if (loser is not null)
            {
                return new CreateBindingOutcome.Duplicate(IntentRepositoryBindingDocumentMapper.ToDomain(loser));
            }
            throw;
        }

        return new CreateBindingOutcome.Created(binding);
    }

    public async Task<IntentRepositoryBinding?> GetByIdAsync(BindingId id, CancellationToken ct)
    {
        var doc = await FindByIdAsync(id.Value, ct);
        return doc is null ? null : IntentRepositoryBindingDocumentMapper.ToDomain(doc);
    }

    public async Task<IReadOnlyList<IntentRepositoryBinding>> FindByIntentAsync(
        IntentId intentId,
        CancellationToken ct)
    {
        var filter = Builders<IntentRepositoryBindingDocument>.Filter.Eq(d => d.IntentId, intentId.Value);
        var docs = await Find(filter)
            .SortBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return docs.Select(IntentRepositoryBindingDocumentMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<IntentRepositoryBinding>> FindAllAsync(CancellationToken ct)
    {
        var docs = await Find(Builders<IntentRepositoryBindingDocument>.Filter.Empty)
            .SortBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return docs.Select(IntentRepositoryBindingDocumentMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<IntentRepositoryBinding>> FindOpenForSyncAsync(CancellationToken ct)
    {
        var fb = Builders<IntentRepositoryBindingDocument>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.CloneStatus, CloneStatusNames.Ready),
            fb.Ne(d => d.PullRequestNumber, null),
            fb.Or(
                fb.Eq(d => d.PullRequestState, PullRequestStateNames.Open),
                fb.Eq(d => d.PullRequestState, null)));

        // Ascending by LastSyncedAt: Mongo sorts null before non-null values, so bindings
        // that have never been polled go first — matches the «oldest poll wins» policy
        // of PullRequestSyncService.
        var docs = await Find(filter)
            .Sort(Builders<IntentRepositoryBindingDocument>.Sort.Ascending(d => d.LastSyncedAt))
            .ToListAsync(ct);
        return docs.Select(IntentRepositoryBindingDocumentMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<IntentRepositoryBinding>> FindReadyWithoutPullRequestAsync(CancellationToken ct)
    {
        var fb = Builders<IntentRepositoryBindingDocument>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.CloneStatus, CloneStatusNames.Ready),
            fb.Eq(d => d.PullRequestNumber, null));

        var docs = await Find(filter)
            .Sort(Builders<IntentRepositoryBindingDocument>.Sort.Ascending(d => d.UpdatedAt))
            .ToListAsync(ct);
        return docs.Select(IntentRepositoryBindingDocumentMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<IntentRepositoryBinding>> FindByCloneStatusAsync(
        string cloneStatus,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cloneStatus);
        var filter = Builders<IntentRepositoryBindingDocument>.Filter.Eq(d => d.CloneStatus, cloneStatus);
        var docs = await Find(filter)
            .SortBy(d => d.CreatedAt)
            .ToListAsync(ct);
        return docs.Select(IntentRepositoryBindingDocumentMapper.ToDomain).ToList();
    }

    public async Task<SaveBindingOutcome> SaveAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);

        var update = Builders<IntentRepositoryBindingDocument>.Update
            .Set(d => d.DefaultBranch, binding.State.DefaultBranch)
            .Set(d => d.CloneStatus, binding.State.CloneStatus)
            .Set(d => d.CloneError, binding.State.CloneError)
            .Set(d => d.PullRequestNumber, binding.State.PullRequestNumber)
            .Set(d => d.PullRequestState, binding.State.PullRequestState)
            .Set(d => d.ReviewCommentsEtag, binding.State.ReviewCommentsEtag)
            .Set(d => d.LastSyncedAt, binding.State.LastSyncedAt?.UtcDateTime)
            .Set(d => d.UpdatedAt, binding.State.UpdatedAt.UtcDateTime);

        // Save isn't a CAS — we must distinguish "no matching id" from "matched but
        // server saw identical values" (the base TryUpdateAsync only sees ModifiedCount).
        // So go through the collection directly for the matched-count check.
        var filter = ById(binding.Id.Value);
        var session = Sessions.Current;
        var result = session is null
            ? await Collection.UpdateOneAsync(filter, update, options: null, ct)
            : await Collection.UpdateOneAsync(session, filter, update, options: null, ct);

        if (result.MatchedCount == 0)
        {
            return new SaveBindingOutcome.NotFound();
        }
        return new SaveBindingOutcome.Saved(binding);
    }

    public async Task<SaveBindingOutcome> ClaimCloningAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);

        // CAS: the precondition `clone_status == pending` lives in the *filter*, so the update
        // matches at most one worker even when the same binding was dequeued twice. The loser's
        // MatchedCount == 0 ⇒ NotFound ⇒ skip the clone (see IIntentRepositoryBindingRepository).
        var fb = Builders<IntentRepositoryBindingDocument>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.Id, binding.Id.Value),
            fb.Eq(d => d.CloneStatus, CloneStatusNames.Pending));

        var update = Builders<IntentRepositoryBindingDocument>.Update
            .Set(d => d.CloneStatus, binding.State.CloneStatus)
            .Set(d => d.CloneError, binding.State.CloneError)
            .Set(d => d.UpdatedAt, binding.State.UpdatedAt.UtcDateTime);

        // Same MatchedCount-vs-ModifiedCount caveat as SaveAsync: a worker that re-sets
        // identical fields would still hold the claim, but ModifiedCount would be 0.
        var session = Sessions.Current;
        var result = session is null
            ? await Collection.UpdateOneAsync(filter, update, options: null, ct)
            : await Collection.UpdateOneAsync(session, filter, update, options: null, ct);

        if (result.MatchedCount == 0)
        {
            return new SaveBindingOutcome.NotFound();
        }
        return new SaveBindingOutcome.Saved(binding);
    }

    public async Task<DeleteBindingOutcome> DeleteAsync(BindingId id, CancellationToken ct)
    {
        var existing = await FindByIdAsync(id.Value, ct);
        if (existing is null)
        {
            return new DeleteBindingOutcome.NotFound();
        }

        var deleteResult = await DeleteOneAsync(ById(id.Value), ct);
        if (deleteResult.DeletedCount == 0)
        {
            return new DeleteBindingOutcome.NotFound();
        }
        return new DeleteBindingOutcome.Deleted(IntentRepositoryBindingDocumentMapper.ToDomain(existing));
    }

    private Task<IntentRepositoryBindingDocument?> FindByCoordinateAsync(
        IntentId intentId,
        RepoCoordinate coordinate,
        CancellationToken ct)
    {
        var fb = Builders<IntentRepositoryBindingDocument>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.IntentId, intentId.Value),
            fb.Eq(d => d.Provider, coordinate.Provider),
            fb.Eq(d => d.Owner, coordinate.Owner),
            fb.Eq(d => d.Repo, coordinate.Repo));
        return FindOneAsync(filter, ct);
    }
}

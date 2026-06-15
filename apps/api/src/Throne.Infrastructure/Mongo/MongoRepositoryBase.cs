using MongoDB.Driver;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Shared scaffolding for Mongo-backed repositories that operate on a single primary
/// collection. Centralises three things that used to be copy-pasted everywhere:
///
/// 1. <see cref="Collection"/> getter via <see cref="IMongoDatabase.GetCollection{TDoc}(string, MongoCollectionSettings)"/>.
/// 2. Session-awareness — every helper transparently routes through
///    <see cref="MongoSessionAccessor.Current"/> when the caller is inside
///    <c>IUnitOfWork.ExecuteAsync</c>, and runs sessionless otherwise.
/// 3. The CAS primitive <see cref="TryUpdateAsync"/>: the caller bakes the
///    version/status guard into the filter and decides Conflict-vs-NotFound after
///    a re-read. The base class deliberately has no retry loop —
///    <c>MongoIntentStatusMutator</c> owns its own bounded retry.
///
/// Repos with secondary collections (links / pins) or a GridFS bucket keep those as
/// regular private fields; only the primary collection is wired through the base.
/// </summary>
internal abstract class MongoRepositoryBase<TDoc, TKey>
    where TDoc : class
{
    protected MongoRepositoryBase(IMongoDatabase database, string collectionName, MongoSessionAccessor sessions)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        ArgumentNullException.ThrowIfNull(sessions);

        Collection = database.GetCollection<TDoc>(collectionName);
        Sessions = sessions;
    }

    protected IMongoCollection<TDoc> Collection { get; }
    protected MongoSessionAccessor Sessions { get; }

    /// <summary>
    /// Each repository defines its own key-field projection (typically
    /// <c>d =&gt; d.Id == id</c>). The base layer only sees an opaque filter and
    /// does not assume the key field is named <c>Id</c>.
    /// </summary>
    protected abstract FilterDefinition<TDoc> ById(TKey id);

    protected async Task<TDoc?> FindOneAsync(FilterDefinition<TDoc> filter, CancellationToken ct)
    {
        var session = Sessions.Current;
        // FirstOrDefaultAsync returns TDoc with a nullable runtime contract but a
        // non-nullable static type — bridge it explicitly so callers see TDoc?.
        return session is null
            ? await Collection.Find(filter).FirstOrDefaultAsync(ct)
            : await Collection.Find(session, filter).FirstOrDefaultAsync(ct);
    }

    protected Task<TDoc?> FindByIdAsync(TKey id, CancellationToken ct) =>
        FindOneAsync(ById(id), ct);

    protected IFindFluent<TDoc, TDoc> Find(FilterDefinition<TDoc> filter)
    {
        var session = Sessions.Current;
        return session is null
            ? Collection.Find(filter)
            : Collection.Find(session, filter);
    }

    /// <summary>
    /// CAS write. <paramref name="filter"/> must already include any precondition
    /// (version, status, etc.); returns <c>true</c> iff exactly one document was
    /// modified. <c>false</c> means the caller has to re-read and decide whether
    /// this is a Conflict or NotFound — the base intentionally does not commit to
    /// an outcome shape and does not retry.
    /// </summary>
    protected async Task<bool> TryUpdateAsync(
        FilterDefinition<TDoc> filter,
        UpdateDefinition<TDoc> update,
        CancellationToken ct)
    {
        var session = Sessions.Current;
        var result = session is null
            ? await Collection.UpdateOneAsync(filter, update, options: null, ct)
            : await Collection.UpdateOneAsync(session, filter, update, options: null, ct);
        return result.ModifiedCount > 0;
    }

    protected Task InsertOneAsync(TDoc document, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(document);
        var session = Sessions.Current;
        return session is null
            ? Collection.InsertOneAsync(document, options: null, ct)
            : Collection.InsertOneAsync(session, document, options: null, ct);
    }

    protected Task<ReplaceOneResult> ReplaceOneAsync(
        FilterDefinition<TDoc> filter,
        TDoc document,
        ReplaceOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var session = Sessions.Current;
        return session is null
            ? Collection.ReplaceOneAsync(filter, document, options, ct)
            : Collection.ReplaceOneAsync(session, filter, document, options, ct);
    }

    protected Task<DeleteResult> DeleteOneAsync(FilterDefinition<TDoc> filter, CancellationToken ct)
    {
        var session = Sessions.Current;
        return session is null
            ? Collection.DeleteOneAsync(filter, ct)
            : Collection.DeleteOneAsync(session, filter, options: null, ct);
    }
}

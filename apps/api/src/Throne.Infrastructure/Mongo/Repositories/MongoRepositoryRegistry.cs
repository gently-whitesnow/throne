using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

/// <summary>
/// Mongo-backed <see cref="IRepositoryRegistry"/>. Sessions are resolved through
/// <see cref="MongoSessionAccessor"/> so an <see cref="EnsureRepositoryAsync"/> call made
/// inside <see cref="IUnitOfWork.ExecuteAsync"/> joins the surrounding transaction; called
/// bare it runs as a standalone idempotent upsert.
/// </summary>
internal sealed class MongoRepositoryRegistry
    : MongoRepositoryBase<RepositoryDocument, string>, IRepositoryRegistry
{
    public MongoRepositoryRegistry(IMongoDatabase database, MongoSessionAccessor sessions)
        : base(database, MongoCollectionNames.Repositories, sessions)
    {
    }

    protected override FilterDefinition<RepositoryDocument> ById(string id) =>
        Builders<RepositoryDocument>.Filter.Eq(d => d.Id, id);

    public async Task<EnsureRepositoryOutcome> EnsureRepositoryAsync(
        RepoCoordinate coordinate, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(coordinate);

        var existing = await FindByCoordinateAsync(coordinate, ct);
        if (existing is not null)
        {
            return new EnsureRepositoryOutcome.Existed(existing);
        }

        var repository = Repository.Create(RepositoryId.New(), coordinate, now);
        var doc = RepositoryDocumentMapper.ToDocument(repository);
        try
        {
            await InsertOneAsync(doc, ct);
            return new EnsureRepositoryOutcome.Created(repository);
        }
        catch (MongoWriteException ex) when (MongoWriteExceptionHelper.IsDuplicateKey(ex))
        {
            // Lost the race against a concurrent first-appearance: the row now exists but this
            // call did not create it, so it must not raise repository.registered.
            var raced = await FindByCoordinateAsync(coordinate, ct)
                ?? throw new InvalidOperationException(
                    $"Repository '{coordinate.Provider}/{coordinate.Owner}/{coordinate.Repo}' "
                    + "duplicate-key on insert but re-read returned null.");
            return new EnsureRepositoryOutcome.Existed(raced);
        }
    }

    public async Task<Repository?> FindByCoordinateAsync(RepoCoordinate coordinate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        var fb = Builders<RepositoryDocument>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.Provider, coordinate.Provider),
            fb.Eq(d => d.Owner, coordinate.Owner),
            fb.Eq(d => d.Repo, coordinate.Repo));
        var doc = await FindOneAsync(filter, ct);
        return doc is null ? null : RepositoryDocumentMapper.ToDomain(doc);
    }

    public async Task<IReadOnlyList<Repository>> ListAsync(CancellationToken ct)
    {
        var docs = await Find(FilterDefinition<RepositoryDocument>.Empty)
            .SortBy(d => d.Provider).ThenBy(d => d.Owner).ThenBy(d => d.Repo)
            .ToListAsync(ct);
        return docs.Select(RepositoryDocumentMapper.ToDomain).ToList();
    }
}

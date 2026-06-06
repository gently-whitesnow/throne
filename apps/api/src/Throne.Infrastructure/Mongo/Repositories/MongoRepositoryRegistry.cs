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
internal sealed class MongoRepositoryRegistry(
    IMongoDatabase database,
    MongoSessionAccessor sessions) : IRepositoryRegistry
{
    private readonly IMongoCollection<RepositoryDocument> _repositories =
        database.GetCollection<RepositoryDocument>(MongoCollectionNames.Repositories);

    public async Task<Repository> EnsureRepositoryAsync(
        RepoCoordinate coordinate, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(coordinate);

        var existing = await FindByCoordinateAsync(coordinate, ct);
        if (existing is not null)
        {
            return existing;
        }

        var repository = Repository.Create(RepositoryId.New(), coordinate, now);
        var doc = RepositoryDocumentMapper.ToDocument(repository);
        var session = sessions.Current;
        try
        {
            if (session is null)
            {
                await _repositories.InsertOneAsync(doc, options: null, ct);
            }
            else
            {
                await _repositories.InsertOneAsync(session, doc, options: null, ct);
            }
            return repository;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Lost the race against a concurrent first-appearance of the same coordinate.
            return await FindByCoordinateAsync(coordinate, ct)
                ?? throw new InvalidOperationException(
                    $"Repository '{coordinate.Provider}/{coordinate.Owner}/{coordinate.Repo}' "
                    + "duplicate-key on insert but re-read returned null.");
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

        var session = sessions.Current;
        var doc = session is null
            ? await _repositories.Find(filter).FirstOrDefaultAsync(ct)
            : await _repositories.Find(session, filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : RepositoryDocumentMapper.ToDomain(doc);
    }

    public async Task<IReadOnlyList<Repository>> ListAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var find = session is null
            ? _repositories.Find(FilterDefinition<RepositoryDocument>.Empty)
            : _repositories.Find(session, FilterDefinition<RepositoryDocument>.Empty);
        var docs = await find
            .SortBy(d => d.Provider).ThenBy(d => d.Owner).ThenBy(d => d.Repo)
            .ToListAsync(ct);
        return docs.Select(RepositoryDocumentMapper.ToDomain).ToList();
    }
}

using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

/// <summary>
/// Mongo-backed <see cref="IRepositoryArtifactRepository"/>. <see cref="WriteAsync"/> is the
/// single create+update upsert: it writes the current-state document and appends the
/// full-snapshot version row, and enforces the <c>expected_version</c> optimistic-concurrency
/// precondition (ADR-0031 / ADR-0002). Both writes share the ambient
/// <see cref="MongoSessionAccessor"/> session, so the caller's transaction keeps the page
/// and its history consistent.
/// </summary>
internal sealed class MongoRepositoryArtifactStore(
    IMongoDatabase database,
    MongoSessionAccessor sessions) : IRepositoryArtifactRepository
{
    private readonly IMongoCollection<RepositoryArtifactDocument> _artifacts =
        database.GetCollection<RepositoryArtifactDocument>(MongoCollectionNames.RepositoryArtifacts);

    private readonly IMongoCollection<RepositoryArtifactVersionDocument> _versions =
        database.GetCollection<RepositoryArtifactVersionDocument>(MongoCollectionNames.RepositoryArtifactVersions);

    public async Task<WriteRepositoryArtifactOutcome> WriteAsync(
        RepoCoordinate coordinate,
        string slug,
        string title,
        string document,
        string renderHint,
        int? expectedVersion,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(coordinate);

        var existing = await FindDocumentBySlugAsync(coordinate, slug, ct);
        return existing is null
            ? await CreateAsync(coordinate, slug, title, document, renderHint, expectedVersion, now, ct)
            : await UpdateAsync(existing, title, document, renderHint, expectedVersion, now, ct);
    }

    private async Task<WriteRepositoryArtifactOutcome> CreateAsync(
        RepoCoordinate coordinate, string slug, string title, string document, string renderHint,
        int? expectedVersion, DateTimeOffset now, CancellationToken ct)
    {
        // expected_version on a page that does not exist yet means «update a version that
        // isn't there» — surface a conflict against the empty current version (0).
        if (expectedVersion is > 0)
        {
            return new WriteRepositoryArtifactOutcome.VersionConflict(0);
        }

        var artifact = RepositoryArtifact.Create(
            RepositoryArtifactId.New(), coordinate, slug, title, document, renderHint, now);
        var session = sessions.Current;
        try
        {
            var doc = RepositoryArtifactDocumentMapper.ToDocument(artifact);
            if (session is null)
            {
                await _artifacts.InsertOneAsync(doc, options: null, ct);
            }
            else
            {
                await _artifacts.InsertOneAsync(session, doc, options: null, ct);
            }
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Concurrent first write of the same (coordinate, slug) won the race.
            var raced = await FindDocumentBySlugAsync(coordinate, slug, ct);
            return new WriteRepositoryArtifactOutcome.VersionConflict(raced?.Version ?? 0);
        }

        await AppendVersionAsync(RepositoryArtifactVersion.Capture(artifact), ct);
        return new WriteRepositoryArtifactOutcome.Written(artifact, Created: true);
    }

    private async Task<WriteRepositoryArtifactOutcome> UpdateAsync(
        RepositoryArtifactDocument existing, string title, string document, string renderHint,
        int? expectedVersion, DateTimeOffset now, CancellationToken ct)
    {
        if (expectedVersion != existing.Version)
        {
            return new WriteRepositoryArtifactOutcome.VersionConflict(existing.Version);
        }

        var artifact = RepositoryArtifactDocumentMapper.ToDomain(existing);
        artifact.Update(title, document, renderHint, now);

        var update = Builders<RepositoryArtifactDocument>.Update
            .Set(d => d.Title, artifact.Title)
            .Set(d => d.Document, artifact.Document)
            .Set(d => d.RenderHint, artifact.RenderHint)
            .Set(d => d.Version, artifact.Version)
            .Set(d => d.UpdatedAt, artifact.UpdatedAt.UtcDateTime);
        var filter = Builders<RepositoryArtifactDocument>.Filter.And(
            Builders<RepositoryArtifactDocument>.Filter.Eq(d => d.Id, existing.Id),
            Builders<RepositoryArtifactDocument>.Filter.Eq(d => d.Version, existing.Version));

        var session = sessions.Current;
        var result = session is null
            ? await _artifacts.UpdateOneAsync(filter, update, options: null, ct)
            : await _artifacts.UpdateOneAsync(session, filter, update, options: null, ct);

        if (result.MatchedCount == 0)
        {
            // The version moved under us between read and CAS write.
            var fresh = await FindDocumentByIdAsync(existing.Id, ct);
            return new WriteRepositoryArtifactOutcome.VersionConflict(fresh?.Version ?? existing.Version);
        }

        await AppendVersionAsync(RepositoryArtifactVersion.Capture(artifact), ct);
        return new WriteRepositoryArtifactOutcome.Written(artifact, Created: false);
    }

    public async Task<RepositoryArtifact?> FindBySlugAsync(
        RepoCoordinate coordinate, string slug, CancellationToken ct)
    {
        var doc = await FindDocumentBySlugAsync(coordinate, slug, ct);
        return doc is null ? null : RepositoryArtifactDocumentMapper.ToDomain(doc);
    }

    public async Task<IReadOnlyList<RepositoryArtifact>> ListByCoordinateAsync(
        RepoCoordinate coordinate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        var fb = Builders<RepositoryArtifactDocument>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.Provider, coordinate.Provider),
            fb.Eq(d => d.Owner, coordinate.Owner),
            fb.Eq(d => d.Repo, coordinate.Repo));

        var session = sessions.Current;
        var find = session is null ? _artifacts.Find(filter) : _artifacts.Find(session, filter);
        var docs = await find.SortBy(d => d.Slug).ToListAsync(ct);
        return docs.Select(RepositoryArtifactDocumentMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<RepositoryArtifactVersion>> ListVersionsAsync(
        RepositoryArtifactId artifactId, CancellationToken ct)
    {
        var filter = Builders<RepositoryArtifactVersionDocument>.Filter.Eq(d => d.ArtifactId, artifactId.Value);
        var session = sessions.Current;
        var find = session is null ? _versions.Find(filter) : _versions.Find(session, filter);
        var docs = await find.SortBy(d => d.Version).ToListAsync(ct);
        return docs.Select(RepositoryArtifactDocumentMapper.ToVersionDomain).ToList();
    }

    private async Task AppendVersionAsync(RepositoryArtifactVersion version, CancellationToken ct)
    {
        var doc = RepositoryArtifactDocumentMapper.ToVersionDocument(version);
        var session = sessions.Current;
        if (session is null)
        {
            await _versions.InsertOneAsync(doc, options: null, ct);
        }
        else
        {
            await _versions.InsertOneAsync(session, doc, options: null, ct);
        }
    }

    private async Task<RepositoryArtifactDocument?> FindDocumentBySlugAsync(
        RepoCoordinate coordinate, string slug, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var fb = Builders<RepositoryArtifactDocument>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.Provider, coordinate.Provider),
            fb.Eq(d => d.Owner, coordinate.Owner),
            fb.Eq(d => d.Repo, coordinate.Repo),
            fb.Eq(d => d.Slug, slug));

        var session = sessions.Current;
        return session is null
            ? await _artifacts.Find(filter).FirstOrDefaultAsync(ct)
            : await _artifacts.Find(session, filter).FirstOrDefaultAsync(ct);
    }

    private async Task<RepositoryArtifactDocument?> FindDocumentByIdAsync(string id, CancellationToken ct)
    {
        var filter = Builders<RepositoryArtifactDocument>.Filter.Eq(d => d.Id, id);
        var session = sessions.Current;
        return session is null
            ? await _artifacts.Find(filter).FirstOrDefaultAsync(ct)
            : await _artifacts.Find(session, filter).FirstOrDefaultAsync(ct);
    }
}

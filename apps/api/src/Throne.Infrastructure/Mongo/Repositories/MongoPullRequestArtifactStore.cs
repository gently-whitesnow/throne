using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

internal sealed class MongoPullRequestArtifactStore
    : MongoRepositoryBase<PullRequestArtifactDocument, string>, IPullRequestArtifactRepository
{
    public MongoPullRequestArtifactStore(IMongoDatabase database, MongoSessionAccessor sessions)
        : base(database, MongoCollectionNames.PullRequestArtifacts, sessions)
    {
    }

    protected override FilterDefinition<PullRequestArtifactDocument> ById(string id) =>
        Builders<PullRequestArtifactDocument>.Filter.Eq(d => d.Id, id);

    public async Task<WritePullRequestArtifactOutcome> UpsertAsync(
        BindingId bindingId,
        int pullRequestNumber,
        string type,
        string render,
        string content,
        string summary,
        string source,
        IReadOnlyList<string> sourceRefs,
        DateTimeOffset producedAt,
        CancellationToken ct)
    {
        var existing = await FindDocumentAsync(bindingId, type, ct);
        return existing is null
            ? await CreateAsync(bindingId, pullRequestNumber, type, render, content, summary, source, sourceRefs, producedAt, ct)
            : await UpdateAsync(existing, pullRequestNumber, render, content, summary, source, sourceRefs, producedAt, ct);
    }

    public async Task<PullRequestArtifact?> GetAsync(BindingId bindingId, string type, CancellationToken ct)
    {
        var doc = await FindDocumentAsync(bindingId, type, ct);
        return doc is null ? null : PullRequestArtifactDocumentMapper.ToDomain(doc);
    }

    public async Task<IReadOnlyList<PullRequestArtifact>> ListAsync(BindingId bindingId, CancellationToken ct)
    {
        var filter = Builders<PullRequestArtifactDocument>.Filter.Eq(d => d.BindingId, bindingId.Value);
        var docs = await Find(filter).SortBy(d => d.Type).ToListAsync(ct);
        return docs.Select(PullRequestArtifactDocumentMapper.ToDomain).ToList();
    }

    private async Task<WritePullRequestArtifactOutcome> CreateAsync(
        BindingId bindingId,
        int pullRequestNumber,
        string type,
        string render,
        string content,
        string summary,
        string source,
        IReadOnlyList<string> sourceRefs,
        DateTimeOffset producedAt,
        CancellationToken ct)
    {
        var artifact = PullRequestArtifact.Create(
            PullRequestArtifactId.New(), bindingId, pullRequestNumber, type, render, content,
            summary, source, sourceRefs, producedAt);
        try
        {
            await InsertOneAsync(PullRequestArtifactDocumentMapper.ToDocument(artifact), ct);
            return new WritePullRequestArtifactOutcome(artifact, Created: true);
        }
        catch (MongoWriteException ex) when (MongoWriteExceptionHelper.IsDuplicateKey(ex))
        {
            var raced = await FindDocumentAsync(bindingId, type, ct);
            if (raced is null)
            {
                throw;
            }
            return await UpdateAsync(raced, pullRequestNumber, render, content, summary, source, sourceRefs, producedAt, ct);
        }
    }

    private async Task<WritePullRequestArtifactOutcome> UpdateAsync(
        PullRequestArtifactDocument existing,
        int pullRequestNumber,
        string render,
        string content,
        string summary,
        string source,
        IReadOnlyList<string> sourceRefs,
        DateTimeOffset producedAt,
        CancellationToken ct)
    {
        var artifact = PullRequestArtifactDocumentMapper.ToDomain(existing);
        artifact.Replace(pullRequestNumber, render, content, summary, source, sourceRefs, producedAt);

        var update = Builders<PullRequestArtifactDocument>.Update
            .Set(d => d.PullRequestNumber, artifact.PullRequestNumber)
            .Set(d => d.Render, artifact.Render)
            .Set(d => d.Content, artifact.Content)
            .Set(d => d.Summary, artifact.Summary)
            .Set(d => d.Source, artifact.Source)
            .Set(d => d.SourceRefs, artifact.SourceRefs.ToArray())
            .Set(d => d.ProducedAt, artifact.ProducedAt.UtcDateTime);

        if (!await TryUpdateAsync(ById(existing.Id), update, ct))
        {
            var fresh = await FindDocumentAsync(artifact.BindingId, artifact.Type, ct);
            if (fresh is null)
            {
                return await CreateAsync(
                    artifact.BindingId, pullRequestNumber, artifact.Type, render, content,
                    summary, source, sourceRefs, producedAt, ct);
            }
            return await UpdateAsync(fresh, pullRequestNumber, render, content, summary, source, sourceRefs, producedAt, ct);
        }

        return new WritePullRequestArtifactOutcome(artifact, Created: false);
    }

    private Task<PullRequestArtifactDocument?> FindDocumentAsync(
        BindingId bindingId,
        string type,
        CancellationToken ct)
    {
        var fb = Builders<PullRequestArtifactDocument>.Filter;
        var filter = fb.And(fb.Eq(d => d.BindingId, bindingId.Value), fb.Eq(d => d.Type, type));
        return FindOneAsync(filter, ct);
    }
}

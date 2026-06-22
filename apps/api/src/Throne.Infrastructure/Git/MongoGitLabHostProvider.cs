using MongoDB.Driver;
using Throne.Application.Git;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Git;

/// <summary>
/// Singleton-backed Mongo provider for the <c>glab</c> hostname. Lazy-materialises
/// <see cref="GitLabHostNormalizer.Default"/> on first read if no doc exists, then
/// caches the value in-process. Cache is invalidated on every successful
/// <see cref="SetHostAsync"/>. No <c>IConfiguration</c> / env-var fallback.
/// </summary>
internal sealed class MongoGitLabHostProvider : IGitLabHostProvider
{
    private readonly IMongoCollection<GitLabHostDocument> _collection;
    private readonly Lock _gate = new();
    private string? _cached;

    public MongoGitLabHostProvider(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _collection = database.GetCollection<GitLabHostDocument>(MongoCollectionNames.GitLabHostSettings);
    }

    public async Task<string> GetHostAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (_cached is { Length: > 0 })
            {
                return _cached;
            }
        }
        var filter = Builders<GitLabHostDocument>.Filter.Eq(d => d.Id, GitLabHostDocument.SingletonId);
        var doc = await _collection.Find(filter).FirstOrDefaultAsync(ct);
        string host;
        if (doc is { Host: { Length: > 0 } existing })
        {
            host = existing;
        }
        else
        {
            host = GitLabHostNormalizer.Default;
            var update = Builders<GitLabHostDocument>.Update
                .SetOnInsert(d => d.Id, GitLabHostDocument.SingletonId)
                .SetOnInsert(d => d.Host, host);
            await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
        }
        lock (_gate)
        {
            _cached = host;
        }
        return host;
    }

    public async Task<string> SetHostAsync(string host, CancellationToken ct)
    {
        var normalized = GitLabHostNormalizer.Validate(host);
        var filter = Builders<GitLabHostDocument>.Filter.Eq(d => d.Id, GitLabHostDocument.SingletonId);
        var update = Builders<GitLabHostDocument>.Update
            .SetOnInsert(d => d.Id, GitLabHostDocument.SingletonId)
            .Set(d => d.Host, normalized);
        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
        lock (_gate)
        {
            _cached = normalized;
        }
        return normalized;
    }
}

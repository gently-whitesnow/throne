using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Capabilities;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoCapabilitiesRepository(IMongoDatabase database, MongoSessionAccessor sessions)
    : ICapabilitiesRepository
{
    private readonly IMongoCollection<CapabilitiesDocument> _collection =
        database.GetCollection<CapabilitiesDocument>(MongoCollectionNames.Settings);

    public async Task<Capabilities?> GetAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var doc = session is null
            ? await _collection.Find(d => d.Id == Capabilities.SingletonId).FirstOrDefaultAsync(ct)
            : await _collection.Find(session, d => d.Id == Capabilities.SingletonId).FirstOrDefaultAsync(ct);
        return doc is null ? null : MapToDomain(doc);
    }

    public async Task SaveAsync(Capabilities capabilities, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoCapabilitiesRepository.SaveAsync must run inside IUnitOfWork.ExecuteAsync.");

        var update = Builders<CapabilitiesDocument>.Update
            .Set(d => d.CurrentVersion, capabilities.CurrentVersion)
            .Set(d => d.UpdatedAt, capabilities.UpdatedAt.UtcDateTime)
            .Set(d => d.Toggles, new Dictionary<string, bool>(capabilities.Toggles, StringComparer.Ordinal))
            .SetOnInsert(d => d.Id, Capabilities.SingletonId);

        await _collection.UpdateOneAsync(
            session,
            d => d.Id == Capabilities.SingletonId,
            update,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    private static Capabilities MapToDomain(CapabilitiesDocument doc)
    {
        var toggles = doc.Toggles is { Count: > 0 }
            ? new Dictionary<string, bool>(doc.Toggles, StringComparer.Ordinal)
            : new Dictionary<string, bool>(StringComparer.Ordinal);
        return Capabilities.Restore(
            currentVersion: doc.CurrentVersion < 1 ? 1 : doc.CurrentVersion,
            updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc),
            toggles: toggles);
    }
}

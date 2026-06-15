using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Capabilities;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoCapabilitiesRepository
    : MongoRepositoryBase<CapabilitiesDocument, string>, ICapabilitiesRepository
{
    public MongoCapabilitiesRepository(IMongoDatabase database, MongoSessionAccessor sessions)
        : base(database, MongoCollectionNames.Settings, sessions)
    {
    }

    protected override FilterDefinition<CapabilitiesDocument> ById(string id) =>
        Builders<CapabilitiesDocument>.Filter.Eq(d => d.Id, id);

    public async Task<Capabilities?> GetAsync(CancellationToken ct)
    {
        var doc = await FindByIdAsync(Capabilities.SingletonId, ct);
        return doc is null ? null : MapToDomain(doc);
    }

    public async Task SaveAsync(Capabilities capabilities, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        var session = Sessions.Current
            ?? throw new InvalidOperationException(
                "MongoCapabilitiesRepository.SaveAsync must run inside IUnitOfWork.ExecuteAsync.");

        var update = Builders<CapabilitiesDocument>.Update
            .Set(d => d.CurrentVersion, capabilities.CurrentVersion)
            .Set(d => d.UpdatedAt, capabilities.UpdatedAt.UtcDateTime)
            .Set(d => d.Toggles, new Dictionary<string, bool>(capabilities.Toggles, StringComparer.Ordinal))
            .SetOnInsert(d => d.Id, Capabilities.SingletonId);

        // Upsert is intentionally not on the base — it's a one-off here.
        await Collection.UpdateOneAsync(
            session,
            ById(Capabilities.SingletonId),
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

using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoTerminalSettingsStore(IMongoDatabase database, MongoSessionAccessor sessions)
    : ITerminalSettingsStore
{
    private readonly IMongoCollection<TerminalSettingsDocument> _collection =
        database.GetCollection<TerminalSettingsDocument>(MongoCollectionNames.Settings);

    public async Task<string> GetDefaultVendorAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var doc = session is null
            ? await _collection.Find(d => d.Id == TerminalSettingsDocument.SingletonId).FirstOrDefaultAsync(ct)
            : await _collection.Find(session, d => d.Id == TerminalSettingsDocument.SingletonId).FirstOrDefaultAsync(ct);

        // Never-written row → native default, so callers never special-case absence.
        return doc is null || !TerminalAgentCatalog.IsKnownVendor(doc.DefaultVendor)
            ? TerminalAgentCatalog.DefaultVendor
            : doc.DefaultVendor;
    }

    public async Task SetDefaultVendorAsync(string vendor, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vendor);
        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoTerminalSettingsStore.SetDefaultVendorAsync must run inside IUnitOfWork.ExecuteAsync.");

        var update = Builders<TerminalSettingsDocument>.Update
            .Set(d => d.DefaultVendor, vendor)
            .SetOnInsert(d => d.Id, TerminalSettingsDocument.SingletonId);

        await _collection.UpdateOneAsync(
            session,
            d => d.Id == TerminalSettingsDocument.SingletonId,
            update,
            new UpdateOptions { IsUpsert = true },
            ct);
    }
}

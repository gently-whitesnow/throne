using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoSkillModeDefaultStore(IMongoDatabase database, MongoSessionAccessor sessions)
    : ISkillModeDefaultStore
{
    private readonly IMongoCollection<SkillModeDefaultDocument> _collection =
        database.GetCollection<SkillModeDefaultDocument>(MongoCollectionNames.SkillModeDefaults);

    public async Task<IReadOnlyList<SkillModeDefault>> ListAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var query = session is null
            ? _collection.Find(Builders<SkillModeDefaultDocument>.Filter.Empty)
            : _collection.Find(session, Builders<SkillModeDefaultDocument>.Filter.Empty);
        var docs = await query.SortBy(d => d.Mode).ThenBy(d => d.SkillId).ToListAsync(ct);
        return docs.Select(ToDomain).ToArray();
    }

    public Task ReplaceAsync(IReadOnlyList<SkillModeDefault> defaults, CancellationToken ct) =>
        UpsertAsync(defaults, SetMode.Upsert, ct);

    public Task UpsertMissingAsync(IReadOnlyList<SkillModeDefault> defaults, CancellationToken ct) =>
        UpsertAsync(defaults, SetMode.SetOnInsert, ct);

    private async Task UpsertAsync(IReadOnlyList<SkillModeDefault> defaults, SetMode mode, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        if (defaults.Count == 0)
        {
            return;
        }

        var writes = defaults.Select(item =>
        {
            var id = IdOf(item.Mode, item.SkillId);
            var update = mode == SetMode.Upsert
                ? Builders<SkillModeDefaultDocument>.Update
                    .SetOnInsert(d => d.Id, id)
                    .Set(d => d.Mode, item.Mode)
                    .Set(d => d.SkillId, item.SkillId)
                    .Set(d => d.Enabled, item.Enabled)
                : Builders<SkillModeDefaultDocument>.Update
                    .SetOnInsert(d => d.Id, id)
                    .SetOnInsert(d => d.Mode, item.Mode)
                    .SetOnInsert(d => d.SkillId, item.SkillId)
                    .SetOnInsert(d => d.Enabled, item.Enabled);
            return new UpdateOneModel<SkillModeDefaultDocument>(
                Builders<SkillModeDefaultDocument>.Filter.Eq(d => d.Id, id),
                update)
            {
                IsUpsert = true,
            };
        }).ToArray();

        var session = sessions.Current;
        if (session is null)
        {
            await _collection.BulkWriteAsync(writes, cancellationToken: ct);
        }
        else
        {
            await _collection.BulkWriteAsync(session, writes, cancellationToken: ct);
        }
    }

    private static SkillModeDefault ToDomain(SkillModeDefaultDocument doc) =>
        new(doc.Mode, doc.SkillId, doc.Enabled);

    internal static string IdOf(string mode, string skillId) => $"{mode}:{skillId}";

    private enum SetMode
    {
        Upsert,
        SetOnInsert,
    }
}

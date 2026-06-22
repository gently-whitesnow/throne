using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Mongo persistence for the per-intent launch axis (ADR-0041). Reads/writes a single
/// upserted document keyed by intent id. The save is auxiliary UI-prefill state outside the
/// spawn's atomic invariant, so — unlike <see cref="MongoTerminalSettingsStore"/> — it does not
/// require an ambient unit-of-work session and writes directly when none is open.
/// </summary>
internal sealed class MongoIntentTerminalLaunchStore(IMongoDatabase database, MongoSessionAccessor sessions)
    : IIntentTerminalLaunchStore
{
    private readonly IMongoCollection<IntentTerminalLaunchDocument> _collection =
        database.GetCollection<IntentTerminalLaunchDocument>(MongoCollectionNames.TerminalLaunches);

    public async Task<TerminalLaunchRecord?> GetAsync(string intentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var session = sessions.Current;
        var doc = session is null
            ? await _collection.Find(d => d.Id == intentId).FirstOrDefaultAsync(ct)
            : await _collection.Find(session, d => d.Id == intentId).FirstOrDefaultAsync(ct);

        if (doc is null)
        {
            return null;
        }
        var attached = doc.AttachedSkillIds is { Count: > 0 }
            ? (IReadOnlyList<string>)doc.AttachedSkillIds.ToArray()
            : Array.Empty<string>();
        return new TerminalLaunchRecord(doc.Mode, doc.Vendor, doc.Model, doc.Effort, attached);
    }

    public async Task SaveAsync(string intentId, TerminalLaunchRecord record, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentNullException.ThrowIfNull(record);

        var builder = Builders<IntentTerminalLaunchDocument>.Update;
        var update = builder
            .SetOnInsert(d => d.Id, intentId)
            .Set(d => d.Mode, record.Mode)
            .Set(d => d.Vendor, record.Vendor)
            .Set(d => d.Model, record.Model);
        // Effort-less vendors (opencode) carry no effort: drop the field rather than store BSON
        // null, so a later switch to an effort vendor never reads a stale value.
        update = record.Effort is { } effort
            ? update.Set(d => d.Effort, effort)
            : update.Unset(d => d.Effort);
        // attached_skill_ids is intentionally NOT touched here: the run/restart pipeline does not
        // overwrite hot-attached skills — they live independently via SetAttachedSkillIdsAsync.
        var options = new UpdateOptions { IsUpsert = true };

        var session = sessions.Current;
        if (session is null)
        {
            await _collection.UpdateOneAsync(d => d.Id == intentId, update, options, ct);
        }
        else
        {
            await _collection.UpdateOneAsync(session, d => d.Id == intentId, update, options, ct);
        }
    }

    public async Task SetAttachedSkillIdsAsync(
        string intentId,
        IReadOnlyList<string> attachedSkillIds,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentNullException.ThrowIfNull(attachedSkillIds);

        var builder = Builders<IntentTerminalLaunchDocument>.Update;
        var update = attachedSkillIds.Count == 0
            ? builder.Unset(d => d.AttachedSkillIds)
            : builder.Set(d => d.AttachedSkillIds, attachedSkillIds.ToList());
        var options = new UpdateOptions { IsUpsert = false };

        var session = sessions.Current;
        if (session is null)
        {
            await _collection.UpdateOneAsync(d => d.Id == intentId, update, options, ct);
        }
        else
        {
            await _collection.UpdateOneAsync(session, d => d.Id == intentId, update, options, ct);
        }
    }
}

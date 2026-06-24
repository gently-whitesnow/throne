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
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptySelections =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    private readonly IMongoCollection<IntentTerminalLaunchDocument> _collection =
        database.GetCollection<IntentTerminalLaunchDocument>(MongoCollectionNames.TerminalLaunches);

    public async Task<TerminalLaunchRecord?> GetAsync(string intentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var doc = await FindAsync(intentId, ct);
        if (doc is null)
        {
            return null;
        }
        var attached = doc.AttachedSkillIds is { Count: > 0 }
            ? (IReadOnlyList<string>)doc.AttachedSkillIds.ToArray()
            : Array.Empty<string>();
        var selections = doc.SelectedSkillIdsByMode is { Count: > 0 }
            ? doc.SelectedSkillIdsByMode.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<string>)kv.Value.ToArray(),
                StringComparer.Ordinal)
            : EmptySelections;
        return new TerminalLaunchRecord(doc.Mode, doc.Vendor, doc.Model, doc.Effort, attached, selections);
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
        // attached_skill_ids / selected_skill_ids_by_mode are intentionally NOT touched here:
        // the run pipeline never overwrites hot-attached skills or per-mode selection through
        // SaveAsync — those have their own dedicated methods.

        await UpdateOneAsync(intentId, update, isUpsert: true, ct);
    }

    public async Task SaveSelectedSkillIdsAsync(
        string intentId,
        string mode,
        IReadOnlyList<string> selectedSkillIds,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(selectedSkillIds);

        var path = SelectionFieldPath(mode);
        var update = Builders<IntentTerminalLaunchDocument>.Update
            .Set(path, selectedSkillIds.Distinct(StringComparer.Ordinal).ToList());

        await UpdateOneAsync(intentId, update, isUpsert: true, ct);
    }

    public async Task SetAttachedSkillIdsAsync(
        string intentId,
        string mode,
        IReadOnlyList<string> attachedSkillIds,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(attachedSkillIds);

        var builder = Builders<IntentTerminalLaunchDocument>.Update;
        UpdateDefinition<IntentTerminalLaunchDocument> update;
        if (attachedSkillIds.Count == 0)
        {
            update = builder.Unset(d => d.AttachedSkillIds);
        }
        else
        {
            var deduped = attachedSkillIds.Distinct(StringComparer.Ordinal).ToList();
            update = builder.Combine(
                builder.Set(d => d.AttachedSkillIds, deduped),
                builder.AddToSetEach(SelectionFieldPath(mode), deduped));
        }

        await UpdateOneAsync(intentId, update, isUpsert: false, ct);
    }

    private static string SelectionFieldPath(string mode) => $"selected_skill_ids_by_mode.{mode}";

    private async Task<IntentTerminalLaunchDocument?> FindAsync(string intentId, CancellationToken ct)
    {
        var session = sessions.Current;
        return session is null
            ? await _collection.Find(d => d.Id == intentId).FirstOrDefaultAsync(ct)
            : await _collection.Find(session, d => d.Id == intentId).FirstOrDefaultAsync(ct);
    }

    private async Task UpdateOneAsync(
        string intentId,
        UpdateDefinition<IntentTerminalLaunchDocument> update,
        bool isUpsert,
        CancellationToken ct)
    {
        var options = new UpdateOptions { IsUpsert = isUpsert };
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

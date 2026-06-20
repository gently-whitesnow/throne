using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentSkillModeSelectionStore(IMongoDatabase database, MongoSessionAccessor sessions)
    : IIntentSkillModeSelectionStore
{
    private readonly IMongoCollection<IntentSkillModeSelectionDocument> _collection =
        database.GetCollection<IntentSkillModeSelectionDocument>(MongoCollectionNames.SkillModeSelections);

    public async Task<IReadOnlyList<string>?> GetAsync(string intentId, string mode, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);

        var session = sessions.Current;
        var doc = session is null
            ? await _collection.Find(d => d.Id == intentId).FirstOrDefaultAsync(ct)
            : await _collection.Find(session, d => d.Id == intentId).FirstOrDefaultAsync(ct);
        return doc?.ModeSelections
            .FirstOrDefault(e => string.Equals(e.Mode, mode, StringComparison.Ordinal))
            ?.SelectedSkillIds
            .ToArray();
    }

    public async Task SaveAsync(
        string intentId,
        string mode,
        IReadOnlyList<string> selectedSkillIds,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(selectedSkillIds);

        var session = sessions.Current;
        var doc = session is null
            ? await _collection.Find(d => d.Id == intentId).FirstOrDefaultAsync(ct)
            : await _collection.Find(session, d => d.Id == intentId).FirstOrDefaultAsync(ct);
        doc ??= new IntentSkillModeSelectionDocument { Id = intentId };
        doc.ModeSelections.RemoveAll(e => string.Equals(e.Mode, mode, StringComparison.Ordinal));
        doc.ModeSelections.Add(new IntentSkillModeSelectionEntryDocument
        {
            Mode = mode,
            SelectedSkillIds = selectedSkillIds.Distinct(StringComparer.Ordinal).ToList(),
        });

        var options = new ReplaceOptions { IsUpsert = true };
        if (session is null)
        {
            await _collection.ReplaceOneAsync(d => d.Id == intentId, doc, options, ct);
        }
        else
        {
            await _collection.ReplaceOneAsync(session, d => d.Id == intentId, doc, options, ct);
        }
    }
}

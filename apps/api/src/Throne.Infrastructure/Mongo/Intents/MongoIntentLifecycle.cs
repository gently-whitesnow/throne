using MongoDB.Driver;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Documents;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Mongo.Intents;

internal sealed class MongoIntentLifecycle(
    IMongoDatabase database,
    MongoSessionAccessor sessions,
    IIntentEventRepository intentEvents)
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    private readonly IMongoCollection<IntentStatusChangeDocument> _statusChanges =
        database.GetCollection<IntentStatusChangeDocument>(MongoCollectionNames.IntentStatusChanges);

    private readonly IMongoCollection<IntentLinkDocument> _intentLinks =
        database.GetCollection<IntentLinkDocument>(MongoCollectionNames.IntentLinks);

    private readonly IMongoCollection<IntentEventDocument> _intentEventDocs =
        database.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents);

    public async Task<CreateIntentOutcome> CreateAsync(
        Intent intent,
        TextVersion initialVersion,
        IntentStatusChange initialStatusChange,
        IReadOnlyList<Tag> upsertedTags,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(initialVersion);
        ArgumentNullException.ThrowIfNull(initialStatusChange);
        ArgumentNullException.ThrowIfNull(upsertedTags);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentLifecycle.CreateAsync must run inside IUnitOfWork.ExecuteAsync.");

        await intentEvents.AppendAsync(
            IntentEvent.ForText(
                Guid.NewGuid().ToString("N"),
                intent.Id,
                initialVersion,
                initialVersion.ChangedAt),
            ct);
        await _intents.InsertOneAsync(session, IntentDocumentMapper.ToDocument(intent), options: null, ct);
        await _statusChanges.InsertOneAsync(session, IntentDocumentMapper.ToDocument(initialStatusChange), options: null, ct);
        return new CreateIntentOutcome(intent, upsertedTags);
    }

    public async Task<DeleteIntentOutcome> DeleteAsync(IntentId id, CancellationToken ct)
    {
        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentLifecycle.DeleteAsync must run inside IUnitOfWork.ExecuteAsync.");

        var byId = IntentCollectionFilters.ById(id.Value);

        var deleteIntent = await _intents.DeleteOneAsync(session, byId, options: null, ct);
        if (deleteIntent.DeletedCount == 0)
        {
            return new DeleteIntentOutcome.NotFound();
        }

        // Cascade events: drop the unified history of the dead intent (both as
        // primary subject and as peer of any link event).
        var eventFilter = Builders<IntentEventDocument>.Filter.Or(
            Builders<IntentEventDocument>.Filter.Eq(d => d.IntentId, id.Value),
            Builders<IntentEventDocument>.Filter.Eq(d => d.PeerIntentId, id.Value));
        await _intentEventDocs.DeleteManyAsync(session, eventFilter, options: null, ct);

        // Cascade: drop incoming + outgoing edges so the graph never references a ghost intent.
        var linkFilter = Builders<IntentLinkDocument>.Filter.Or(
            Builders<IntentLinkDocument>.Filter.Eq(d => d.FromId, id.Value),
            Builders<IntentLinkDocument>.Filter.Eq(d => d.ToId, id.Value));
        await _intentLinks.DeleteManyAsync(session, linkFilter, options: null, ct);

        return new DeleteIntentOutcome.Deleted(id.Value);
    }
}

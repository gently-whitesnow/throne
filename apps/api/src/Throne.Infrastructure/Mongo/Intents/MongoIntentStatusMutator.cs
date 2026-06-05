using MongoDB.Driver;
using Throne.Application.Auth;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Intents;

internal sealed class MongoIntentStatusMutator(
    IMongoDatabase database,
    MongoSessionAccessor sessions,
    ICurrentUserAccessor currentUser,
    IIntentEventRepository intentEvents)
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    private readonly IMongoCollection<IntentStatusChangeDocument> _statusChanges =
        database.GetCollection<IntentStatusChangeDocument>(MongoCollectionNames.IntentStatusChanges);

    public async Task<SetIntentStatusOutcome> SetStatusAsync(
        IntentId id,
        string status,
        string? appendText,
        string? reason,
        IntentTrainingAuthor changedBy,
        string source,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentStatusMutator.SetStatusAsync must run inside IUnitOfWork.ExecuteAsync.");

        var byIdAndOwner = IntentCollectionFilters.ByIdAndOwner(id.Value, currentUser.UserId);

        var document = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new SetIntentStatusOutcome.NotFound();
        }

        var intent = IntentDocumentMapper.ToDomain(document);
        var originalVersion = intent.State.CurrentVersion;
        var originalStatus = intent.State.Status;

        var textVersion = ApplyOptionalAppend(intent, appendText, changedBy, now);
        var statusChanged = intent.SetStatus(status, now);
        if (!statusChanged && textVersion is null)
        {
            return new SetIntentStatusOutcome.Updated(intent);
        }

        var updateResult = await _intents.UpdateOneAsync(
            session,
            IntentCollectionFilters.BuildStatusUpdateFilter(id.Value, originalVersion, originalStatus, currentUser.UserId),
            BuildStatusUpdate(intent),
            options: null,
            ct);

        if (updateResult.ModifiedCount == 0)
        {
            var fresh = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
            if (fresh is null)
            {
                return new SetIntentStatusOutcome.NotFound();
            }
            return new SetIntentStatusOutcome.Conflict(fresh.CurrentVersion, fresh.Status);
        }

        if (textVersion is not null)
        {
            await intentEvents.AppendAsync(
                IntentEvent.ForText(
                    Guid.NewGuid().ToString("N"),
                    intent.Id,
                    textVersion,
                    textVersion.ChangedAt),
                ct);
        }

        if (statusChanged)
        {
            var statusChange = IntentStatusChange.Create(
                id: Guid.NewGuid().ToString("N"),
                intentId: id,
                intentVersionAtWrite: intent.State.CurrentVersion,
                fromStatus: originalStatus,
                toStatus: intent.State.Status,
                source: source,
                createdAt: now,
                createdBy: changedBy,
                reason: reason);
            await _statusChanges.InsertOneAsync(session, IntentDocumentMapper.ToDocument(statusChange), options: null, ct);
        }

        return new SetIntentStatusOutcome.Updated(intent);
    }

    public async Task<SetIntentStatusOutcome> SetStatusBySystemAsync(
        IntentId id,
        string status,
        string? reason,
        string source,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentStatusMutator.SetStatusBySystemAsync must run inside IUnitOfWork.ExecuteAsync.");

        var fb = Builders<IntentDocument>.Filter;
        var byId = fb.Eq(d => d.Id, id.Value);
        var document = await _intents.Find(session, byId).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new SetIntentStatusOutcome.NotFound();
        }

        var intent = IntentDocumentMapper.ToDomain(document);
        var originalStatus = intent.State.Status;
        var originalVersion = intent.State.CurrentVersion;
        if (!intent.SetStatus(status, now))
        {
            return new SetIntentStatusOutcome.Updated(intent);
        }

        var updateFilter = fb.And(
            byId,
            fb.Eq(d => d.CurrentVersion, originalVersion),
            fb.Eq(d => d.Status, originalStatus));
        var updateResult = await _intents.UpdateOneAsync(
            session, updateFilter, BuildStatusUpdate(intent), options: null, ct);
        if (updateResult.ModifiedCount == 0)
        {
            var fresh = await _intents.Find(session, byId).FirstOrDefaultAsync(ct);
            return fresh is null
                ? new SetIntentStatusOutcome.NotFound()
                : new SetIntentStatusOutcome.Conflict(fresh.CurrentVersion, fresh.Status);
        }

        var statusChange = IntentStatusChange.Create(
            id: Guid.NewGuid().ToString("N"),
            intentId: id,
            intentVersionAtWrite: intent.State.CurrentVersion,
            fromStatus: originalStatus,
            toStatus: intent.State.Status,
            source: source,
            createdAt: now,
            createdBy: IntentTrainingAuthor.System,
            reason: reason);
        await _statusChanges.InsertOneAsync(session, IntentDocumentMapper.ToDocument(statusChange), options: null, ct);

        return new SetIntentStatusOutcome.Updated(intent);
    }

    private static TextVersion? ApplyOptionalAppend(
        Intent intent,
        string? appendText,
        IntentTrainingAuthor changedBy,
        DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(appendText))
        {
            return null;
        }
        var appendResult = intent.AppendText(
            appendText,
            Guid.NewGuid().ToString("N"),
            now,
            IntentDocumentMapper.ToTextVersionAuthor(changedBy));
        if (appendResult is not InsertTextResult.Inserted inserted)
        {
            throw new InvalidOperationException($"Unexpected append result: {appendResult.GetType().Name}");
        }
        return inserted.Version;
    }

    private static UpdateDefinition<IntentDocument> BuildStatusUpdate(Intent intent) =>
        Builders<IntentDocument>.Update
            .Set(d => d.Text, intent.State.Text)
            .Set(d => d.Status, intent.State.Status)
            .Set(d => d.CurrentVersion, intent.State.CurrentVersion)
            .Set(d => d.UpdatedAt, intent.State.UpdatedAt.UtcDateTime);

    public async Task<SetIntentTagsOutcome> SetTagsAsync(
        IntentId id,
        int expectedVersion,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tagIds);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentStatusMutator.SetTagsAsync must run inside IUnitOfWork.ExecuteAsync.");

        var byIdAndOwner = IntentCollectionFilters.ByIdAndOwner(id.Value, currentUser.UserId);
        var document = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new SetIntentTagsOutcome.NotFound();
        }
        if (document.CurrentVersion != expectedVersion)
        {
            return new SetIntentTagsOutcome.VersionConflict(document.CurrentVersion);
        }

        var intent = IntentDocumentMapper.ToDomain(document);
        var changed = intent.SetTags(tagIds, now);
        if (!changed)
        {
            return new SetIntentTagsOutcome.Updated(intent, Changed: false);
        }

        var update = Builders<IntentDocument>.Update
            .Set(d => d.TagIds, intent.TagIds.Select(t => t.Value).ToList())
            .Set(d => d.UpdatedAt, intent.State.UpdatedAt.UtcDateTime);

        var updateFilter = Builders<IntentDocument>.Filter.And(
            byIdAndOwner,
            Builders<IntentDocument>.Filter.Eq(d => d.CurrentVersion, expectedVersion));
        var updateResult = await _intents.UpdateOneAsync(session, updateFilter, update, options: null, ct);

        if (updateResult.ModifiedCount == 0)
        {
            var fresh = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
            return fresh is null
                ? new SetIntentTagsOutcome.NotFound()
                : new SetIntentTagsOutcome.VersionConflict(fresh.CurrentVersion);
        }

        return new SetIntentTagsOutcome.Updated(intent, Changed: true);
    }
}

using MongoDB.Driver;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Intents;

internal sealed class MongoIntentTextEditor(
    IMongoDatabase database,
    MongoSessionAccessor sessions,
    IIntentEventRepository intentEvents)
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    public async Task<ReplaceIntentTextOutcome> ReplaceTextAsync(
        IntentId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentTextEditor.ReplaceTextAsync must run inside IUnitOfWork.ExecuteAsync.");

        var byId = IntentCollectionFilters.ById(id.Value);
        var document = await _intents.Find(session, byId).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new ReplaceIntentTextOutcome.NotFound();
        }
        if (document.CurrentVersion != expectedVersion)
        {
            return new ReplaceIntentTextOutcome.VersionConflict(document.CurrentVersion);
        }

        var intent = IntentDocumentMapper.ToDomain(document);
        var newVersionId = Guid.NewGuid().ToString("N");
        var domainResult = intent.ReplaceText(oldText, newText, newVersionId, now, changedBy);

        return domainResult switch
        {
            ReplaceTextResult.MatchNotFound m => new ReplaceIntentTextOutcome.MatchNotFound(m.QueryPreview),
            ReplaceTextResult.MatchAmbiguous a => new ReplaceIntentTextOutcome.MatchAmbiguous(a.MatchesCount, a.MatchLines),
            ReplaceTextResult.Replaced replaced => await PersistReplaceAsync(byId, intent, expectedVersion, replaced, session, ct),
            _ => throw new InvalidOperationException($"Unhandled domain result: {domainResult.GetType().Name}"),
        };
    }

    private async Task<ReplaceIntentTextOutcome> PersistReplaceAsync(
        FilterDefinition<IntentDocument> byId,
        Intent intent,
        int expectedVersion,
        ReplaceTextResult.Replaced replaced,
        IClientSessionHandle session,
        CancellationToken ct)
    {
        var update = Builders<IntentDocument>.Update
            .Set(d => d.Text, intent.State.Text)
            .Set(d => d.CurrentVersion, intent.State.CurrentVersion)
            .Set(d => d.UpdatedAt, intent.State.UpdatedAt.UtcDateTime);

        var updateFilter = Builders<IntentDocument>.Filter.And(
            byId,
            Builders<IntentDocument>.Filter.Eq(d => d.CurrentVersion, expectedVersion));
        var updateResult = await _intents.UpdateOneAsync(session, updateFilter, update, options: null, ct);

        if (updateResult.ModifiedCount == 0)
        {
            var fresh = await _intents.Find(session, byId).FirstOrDefaultAsync(ct);
            return new ReplaceIntentTextOutcome.VersionConflict(fresh?.CurrentVersion ?? expectedVersion);
        }

        await intentEvents.AppendAsync(
            IntentEvent.ForText(
                Guid.NewGuid().ToString("N"),
                intent.Id,
                replaced.Version,
                replaced.Version.ChangedAt),
            ct);
        return new ReplaceIntentTextOutcome.Replaced(intent);
    }

    public async Task<InsertIntentTextAfterLineOutcome> InsertTextAfterLineAsync(
        IntentId id,
        int expectedVersion,
        int afterLine,
        string insertText,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(insertText);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentTextEditor.InsertTextAfterLineAsync must run inside IUnitOfWork.ExecuteAsync.");

        var byId = IntentCollectionFilters.ById(id.Value);
        var document = await _intents.Find(session, byId).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new InsertIntentTextAfterLineOutcome.NotFound();
        }
        if (document.CurrentVersion != expectedVersion)
        {
            return new InsertIntentTextAfterLineOutcome.VersionConflict(document.CurrentVersion);
        }

        var intent = IntentDocumentMapper.ToDomain(document);
        var newVersionId = Guid.NewGuid().ToString("N");
        var domainResult = intent.InsertTextAfterLine(afterLine, insertText, newVersionId, now, TextVersionAuthor.Agent);

        return domainResult switch
        {
            InsertTextResult.LineOutOfRange r => new InsertIntentTextAfterLineOutcome.LineOutOfRange(r.TotalLines, r.RequestedAfterLine),
            InsertTextResult.Inserted inserted => await PersistInsertAsync(byId, intent, expectedVersion, inserted, session, ct),
            _ => throw new InvalidOperationException($"Unhandled domain result: {domainResult.GetType().Name}"),
        };
    }

    private async Task<InsertIntentTextAfterLineOutcome> PersistInsertAsync(
        FilterDefinition<IntentDocument> byId,
        Intent intent,
        int expectedVersion,
        InsertTextResult.Inserted inserted,
        IClientSessionHandle session,
        CancellationToken ct)
    {
        var update = Builders<IntentDocument>.Update
            .Set(d => d.Text, intent.State.Text)
            .Set(d => d.CurrentVersion, intent.State.CurrentVersion)
            .Set(d => d.UpdatedAt, intent.State.UpdatedAt.UtcDateTime);

        var updateFilter = Builders<IntentDocument>.Filter.And(
            byId,
            Builders<IntentDocument>.Filter.Eq(d => d.CurrentVersion, expectedVersion));
        var updateResult = await _intents.UpdateOneAsync(session, updateFilter, update, options: null, ct);

        if (updateResult.ModifiedCount == 0)
        {
            var fresh = await _intents.Find(session, byId).FirstOrDefaultAsync(ct);
            return new InsertIntentTextAfterLineOutcome.VersionConflict(fresh?.CurrentVersion ?? expectedVersion);
        }

        await intentEvents.AppendAsync(
            IntentEvent.ForText(
                Guid.NewGuid().ToString("N"),
                intent.Id,
                inserted.Version,
                inserted.Version.ChangedAt),
            ct);
        return new InsertIntentTextAfterLineOutcome.Inserted(intent);
    }
}

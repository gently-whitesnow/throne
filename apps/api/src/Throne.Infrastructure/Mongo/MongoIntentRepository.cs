using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Documents;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentRepository(IMongoDatabase database, MongoSessionAccessor sessions) : IIntentRepository
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    private readonly IMongoCollection<TextVersionDocument> _textVersions =
        database.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions);

    private readonly IMongoCollection<IntentStatusChangeDocument> _statusChanges =
        database.GetCollection<IntentStatusChangeDocument>(MongoCollectionNames.IntentStatusChanges);

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
                "MongoIntentRepository.CreateAsync must run inside IUnitOfWork.ExecuteAsync.");

        await _textVersions.InsertOneAsync(session, MapVersion(initialVersion), options: null, ct);
        await _intents.InsertOneAsync(session, MapIntent(intent), options: null, ct);
        await _statusChanges.InsertOneAsync(session, MapStatusChange(initialStatusChange), options: null, ct)
            ;
        return new CreateIntentOutcome(intent, upsertedTags);
    }

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
                "MongoIntentRepository.ReplaceTextAsync must run inside IUnitOfWork.ExecuteAsync.");

        var document = await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new ReplaceIntentTextOutcome.NotFound();
        }

        if (document.CurrentVersion != expectedVersion)
        {
            return new ReplaceIntentTextOutcome.VersionConflict(document.CurrentVersion);
        }

        var intent = MapToDomain(document);
        var newVersionId = Guid.NewGuid().ToString("N");
        var domainResult = intent.ReplaceText(oldText, newText, newVersionId, now, changedBy);

        switch (domainResult)
        {
            case ReplaceTextResult.MatchNotFound matchNotFound:
                return new ReplaceIntentTextOutcome.MatchNotFound(matchNotFound.QueryPreview);

            case ReplaceTextResult.MatchAmbiguous matchAmbiguous:
                return new ReplaceIntentTextOutcome.MatchAmbiguous(matchAmbiguous.MatchesCount, matchAmbiguous.MatchLines);

            case ReplaceTextResult.Replaced replaced:
                {
                    var update = Builders<IntentDocument>.Update
                        .Set(d => d.Text, intent.Text)
                        .Set(d => d.CurrentVersion, intent.CurrentVersion)
                        .Set(d => d.UpdatedAt, intent.UpdatedAt.UtcDateTime);

                    var updateResult = await _intents.UpdateOneAsync(
                        session,
                        d => d.Id == id.Value && d.CurrentVersion == expectedVersion,
                        update,
                        options: null,
                        ct);

                    if (updateResult.ModifiedCount == 0)
                    {
                        var fresh = await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
                        return new ReplaceIntentTextOutcome.VersionConflict(fresh?.CurrentVersion ?? expectedVersion);
                    }

                    await _textVersions.InsertOneAsync(session, MapVersion(replaced.Version), options: null, ct);
                    return new ReplaceIntentTextOutcome.Replaced(intent);
                }

            default:
                throw new InvalidOperationException($"Unhandled domain result: {domainResult.GetType().Name}");
        }
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
                "MongoIntentRepository.InsertTextAfterLineAsync must run inside IUnitOfWork.ExecuteAsync.");

        var document = await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new InsertIntentTextAfterLineOutcome.NotFound();
        }

        if (document.CurrentVersion != expectedVersion)
        {
            return new InsertIntentTextAfterLineOutcome.VersionConflict(document.CurrentVersion);
        }

        var intent = MapToDomain(document);
        var newVersionId = Guid.NewGuid().ToString("N");
        var domainResult = intent.InsertAfterLine(afterLine, insertText, newVersionId, now, TextVersionAuthor.Agent);

        switch (domainResult)
        {
            case InsertTextResult.LineOutOfRange outOfRange:
                return new InsertIntentTextAfterLineOutcome.LineOutOfRange(outOfRange.TotalLines, outOfRange.RequestedAfterLine);

            case InsertTextResult.Inserted inserted:
                {
                    var update = Builders<IntentDocument>.Update
                        .Set(d => d.Text, intent.Text)
                        .Set(d => d.CurrentVersion, intent.CurrentVersion)
                        .Set(d => d.UpdatedAt, intent.UpdatedAt.UtcDateTime);

                    var updateResult = await _intents.UpdateOneAsync(
                        session,
                        d => d.Id == id.Value && d.CurrentVersion == expectedVersion,
                        update,
                        options: null,
                        ct);

                    if (updateResult.ModifiedCount == 0)
                    {
                        var fresh = await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
                        return new InsertIntentTextAfterLineOutcome.VersionConflict(fresh?.CurrentVersion ?? expectedVersion);
                    }

                    await _textVersions.InsertOneAsync(session, MapVersion(inserted.Version), options: null, ct);
                    return new InsertIntentTextAfterLineOutcome.Inserted(intent);
                }

            default:
                throw new InvalidOperationException($"Unhandled domain result: {domainResult.GetType().Name}");
        }
    }

    public async Task<IReadOnlyList<Intent>> ListAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var documents = session is null
            ? await _intents.Find(FilterDefinition<IntentDocument>.Empty)
                .SortBy(d => d.CreatedAt)
                .ToListAsync(ct)
            : await _intents.Find(session, FilterDefinition<IntentDocument>.Empty)
                .SortBy(d => d.CreatedAt)
                .ToListAsync(ct);

        var result = new List<Intent>(documents.Count);
        foreach (var doc in documents)
        {
            result.Add(MapToDomain(doc));
        }
        return result;
    }

    public async Task<DeleteIntentOutcome> DeleteAsync(IntentId id, CancellationToken ct)
    {
        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentRepository.DeleteAsync must run inside IUnitOfWork.ExecuteAsync.");

        var deleteIntent = await _intents.DeleteOneAsync(session, d => d.Id == id.Value, options: null, ct);
        if (deleteIntent.DeletedCount == 0)
        {
            return new DeleteIntentOutcome.NotFound();
        }

        var ownerKindWire = TextVersionOwnerKind.Intent.ToWire();
        await _textVersions.DeleteManyAsync(
            session,
            v => v.OwnerKind == ownerKindWire && v.OwnerId == id.Value,
            options: null,
            ct);

        return new DeleteIntentOutcome.Deleted(id.Value);
    }

    public async Task<SetIntentStatusOutcome> SetStatusAsync(
        IntentId id,
        string status,
        string? appendText,
        IntentTrainingAuthor changedBy,
        string source,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentRepository.SetStatusAsync must run inside IUnitOfWork.ExecuteAsync.");

        var document = await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new SetIntentStatusOutcome.NotFound();
        }

        var intent = MapToDomain(document);
        var originalVersion = intent.CurrentVersion;
        var originalStatus = intent.Status;

        TextVersion? textVersion = null;
        if (!string.IsNullOrEmpty(appendText))
        {
            var appendResult = intent.AppendText(
                appendText,
                Guid.NewGuid().ToString("N"),
                now,
                ToTextVersionAuthor(changedBy));

            if (appendResult is not InsertTextResult.Inserted inserted)
            {
                throw new InvalidOperationException($"Unexpected append result: {appendResult.GetType().Name}");
            }

            textVersion = inserted.Version;
        }

        var statusChanged = intent.SetStatus(status, now);
        var shouldUpdate = statusChanged || textVersion is not null;
        if (!shouldUpdate)
        {
            return new SetIntentStatusOutcome.Updated(intent);
        }

        var update = Builders<IntentDocument>.Update
            .Set(d => d.Text, intent.Text)
            .Set(d => d.Status, intent.Status)
            .Set(d => d.CurrentVersion, intent.CurrentVersion)
            .Set(d => d.UpdatedAt, intent.UpdatedAt.UtcDateTime);

        var updateResult = await _intents.UpdateOneAsync(
            session,
            BuildStatusUpdateFilter(id.Value, originalVersion, originalStatus),
            update,
            options: null,
            ct);

        if (updateResult.ModifiedCount == 0)
        {
            var fresh = await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
            if (fresh is null)
            {
                return new SetIntentStatusOutcome.NotFound();
            }

            return new SetIntentStatusOutcome.Conflict(fresh.CurrentVersion, fresh.Status);
        }

        if (textVersion is not null)
        {
            await _textVersions.InsertOneAsync(session, MapVersion(textVersion), options: null, ct);
        }

        if (statusChanged)
        {
            var statusChange = IntentStatusChange.Create(
                id: Guid.NewGuid().ToString("N"),
                intentId: id,
                intentVersionAtWrite: intent.CurrentVersion,
                fromStatus: originalStatus,
                toStatus: intent.Status,
                source: source,
                createdAt: now,
                createdBy: changedBy);

            await _statusChanges.InsertOneAsync(session, MapStatusChange(statusChange), options: null, ct)
                ;
        }

        return new SetIntentStatusOutcome.Updated(intent);
    }

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
                "MongoIntentRepository.SetTagsAsync must run inside IUnitOfWork.ExecuteAsync.");

        var document = await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new SetIntentTagsOutcome.NotFound();
        }

        if (document.CurrentVersion != expectedVersion)
        {
            return new SetIntentTagsOutcome.VersionConflict(document.CurrentVersion);
        }

        var intent = MapToDomain(document);
        var changed = intent.SetTagIds(tagIds, now);
        if (!changed)
        {
            return new SetIntentTagsOutcome.Updated(intent, Changed: false);
        }

        var newTagIdValues = intent.TagIds.Select(t => t.Value).ToList();
        var update = Builders<IntentDocument>.Update
            .Set(d => d.TagIds, newTagIdValues)
            .Set(d => d.UpdatedAt, intent.UpdatedAt.UtcDateTime);

        var updateResult = await _intents.UpdateOneAsync(
            session,
            d => d.Id == id.Value && d.CurrentVersion == expectedVersion,
            update,
            options: null,
            ct);

        if (updateResult.ModifiedCount == 0)
        {
            var fresh = await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
            return fresh is null
                ? new SetIntentTagsOutcome.NotFound()
                : new SetIntentTagsOutcome.VersionConflict(fresh.CurrentVersion);
        }

        return new SetIntentTagsOutcome.Updated(intent, Changed: true);
    }

    public async Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var document = session is null
            ? await _intents.Find(d => d.Id == id.Value).FirstOrDefaultAsync(ct)
            : await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);

        return document is null ? null : MapToDomain(document);
    }

    private static IntentDocument MapIntent(Intent intent) => new()
    {
        Id = intent.Id.Value,
        Text = intent.Text,
        Status = intent.Status,
        CurrentVersion = intent.CurrentVersion,
        TagIds = intent.TagIds.Select(t => t.Value).ToList(),
        CreatedAt = intent.CreatedAt.UtcDateTime,
        UpdatedAt = intent.UpdatedAt.UtcDateTime,
    };

    private static TextVersionDocument MapVersion(TextVersion v) => new()
    {
        Id = v.Id,
        OwnerKind = v.OwnerKind.ToWire(),
        OwnerId = v.OwnerId,
        Version = v.Version,
        Kind = v.Kind.ToWire(),
        Snapshot = v.Snapshot,
        OldText = v.OldText,
        NewText = v.NewText,
        AfterLine = v.AfterLine,
        InsertText = v.InsertText,
        ChangedAt = v.ChangedAt.UtcDateTime,
        ChangedBy = v.ChangedBy.ToWire(),
    };

    private static IntentStatusChangeDocument MapStatusChange(IntentStatusChange change) => new()
    {
        Id = change.Id,
        IntentId = change.IntentId.Value,
        IntentVersionAtWrite = change.IntentVersionAtWrite,
        FromStatus = change.FromStatus,
        ToStatus = change.ToStatus,
        Source = change.Source,
        CreatedAt = change.CreatedAt.UtcDateTime,
        CreatedBy = change.CreatedBy.ToWire(),
    };

    private static Intent MapToDomain(IntentDocument doc) => Intent.Restore(
        id: new IntentId(doc.Id),
        text: doc.Text,
        status: string.IsNullOrWhiteSpace(doc.Status) ? IntentStatusNames.Draft : doc.Status,
        currentVersion: doc.CurrentVersion,
        tagIds: doc.TagIds.Select(v => new TagId(v)).ToList(),
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc));

    private static TextVersionAuthor ToTextVersionAuthor(IntentTrainingAuthor author) => author switch
    {
        IntentTrainingAuthor.User => TextVersionAuthor.User,
        IntentTrainingAuthor.Agent => TextVersionAuthor.Agent,
        IntentTrainingAuthor.System => TextVersionAuthor.System,
        _ => throw new InvalidOperationException($"Unknown training author: {author}."),
    };

    private static FilterDefinition<IntentDocument> BuildStatusUpdateFilter(
        string id,
        int currentVersion,
        string originalStatus)
    {
        var filter = Builders<IntentDocument>.Filter;
        var statusFilter = filter.Eq(d => d.Status, originalStatus);
        if (string.Equals(originalStatus, IntentStatusNames.Draft, StringComparison.Ordinal))
        {
            statusFilter = filter.Or(statusFilter, filter.Eq(d => d.Status, string.Empty));
        }

        return filter.And(
            filter.Eq(d => d.Id, id),
            filter.Eq(d => d.CurrentVersion, currentVersion),
            statusFilter);
    }
}

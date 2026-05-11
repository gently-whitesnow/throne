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
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentRepository(
    IMongoDatabase database,
    MongoSessionAccessor sessions,
    ICurrentUserAccessor currentUser,
    IIntentEventRepository intentEvents) : IIntentRepository
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    private readonly IMongoCollection<IntentStatusChangeDocument> _statusChanges =
        database.GetCollection<IntentStatusChangeDocument>(MongoCollectionNames.IntentStatusChanges);

    private readonly IMongoCollection<IntentLinkDocument> _intentLinks =
        database.GetCollection<IntentLinkDocument>(MongoCollectionNames.IntentLinks);

    private readonly IMongoCollection<IntentEventDocument> _intentEventDocs =
        database.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents);

    private FilterDefinition<IntentDocument> OwnerFilter() =>
        Builders<IntentDocument>.Filter.Eq(d => d.OwnerUserId, currentUser.UserId);

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

        await intentEvents.AppendAsync(
            IntentEvent.ForText(
                Guid.NewGuid().ToString("N"),
                intent.Id,
                initialVersion,
                initialVersion.ChangedAt),
            ct);
        await _intents.InsertOneAsync(session, MapIntent(intent), options: null, ct);
        await _statusChanges.InsertOneAsync(session, MapStatusChange(initialStatusChange), options: null, ct)
            ;
        return new CreateIntentOutcome(intent, upsertedTags);
    }

    public async Task<string?> GetMinSortKeyAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var find = session is null
            ? _intents.Find(OwnerFilter())
            : _intents.Find(session, OwnerFilter());
        var doc = await find
            .Sort(Builders<IntentDocument>.Sort.Ascending(d => d.SortKey))
            .Limit(1)
            .Project(d => d.SortKey)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrEmpty(doc) ? null : doc;
    }

    public async Task<MoveIntentOutcome> MoveBetweenAsync(
        IntentId id,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct)
    {
        if (beforeId is null && afterId is null)
        {
            throw new ArgumentException("At least one of beforeId / afterId must be supplied.", nameof(beforeId));
        }

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentRepository.MoveBetweenAsync must run inside IUnitOfWork.ExecuteAsync.");

        // Resolve both pivots in a single round-trip.
        var pivotIds = new List<string>(2);
        if (beforeId is not null)
        {
            pivotIds.Add(beforeId.Value.Value);
        }
        if (afterId is not null)
        {
            pivotIds.Add(afterId.Value.Value);
        }
        var pivotFilter = Builders<IntentDocument>.Filter.And(
            OwnerFilter(),
            Builders<IntentDocument>.Filter.In(d => d.Id, pivotIds));
        var pivotDocs = await _intents.Find(session, pivotFilter)
            .Project(d => new { d.Id, d.SortKey })
            .ToListAsync(ct);

        string? Lookup(IntentId? pivot) => pivot is null
            ? null
            : pivotDocs.FirstOrDefault(d => d.Id == pivot.Value.Value)?.SortKey;
        var beforeKey = Lookup(beforeId);
        var afterKey = Lookup(afterId);
        if (beforeId is not null && beforeKey is null)
        {
            return new MoveIntentOutcome.PivotNotFound(beforeId.Value.Value);
        }
        if (afterId is not null && afterKey is null)
        {
            return new MoveIntentOutcome.PivotNotFound(afterId.Value.Value);
        }

        var byIdAndOwner = Builders<IntentDocument>.Filter.And(
            Builders<IntentDocument>.Filter.Eq(d => d.Id, id.Value),
            OwnerFilter());

        var document = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new MoveIntentOutcome.NotFound();
        }

        var newSortKey = FractionalIndex.Between(beforeKey, afterKey);
        if (string.Equals(document.SortKey, newSortKey, StringComparison.Ordinal))
        {
            return new MoveIntentOutcome.Moved(MapToDomain(document), Changed: false);
        }

        // Reorder is purely positional: do not touch updated_at or current_version.
        var update = Builders<IntentDocument>.Update.Set(d => d.SortKey, newSortKey);
        var updateResult = await _intents.UpdateOneAsync(session, byIdAndOwner, update, options: null, ct);
        if (updateResult.ModifiedCount == 0)
        {
            var fresh = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
            return fresh is null
                ? new MoveIntentOutcome.NotFound()
                : new MoveIntentOutcome.Moved(MapToDomain(fresh), Changed: false);
        }

        document.SortKey = newSortKey;
        return new MoveIntentOutcome.Moved(MapToDomain(document), Changed: true);
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

        var byIdAndOwner = Builders<IntentDocument>.Filter.And(
            Builders<IntentDocument>.Filter.Eq(d => d.Id, id.Value),
            OwnerFilter());

        var document = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
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

                    var updateFilter = Builders<IntentDocument>.Filter.And(
                        byIdAndOwner,
                        Builders<IntentDocument>.Filter.Eq(d => d.CurrentVersion, expectedVersion));
                    var updateResult = await _intents.UpdateOneAsync(
                        session,
                        updateFilter,
                        update,
                        options: null,
                        ct);

                    if (updateResult.ModifiedCount == 0)
                    {
                        var fresh = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
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

        var byIdAndOwner = Builders<IntentDocument>.Filter.And(
            Builders<IntentDocument>.Filter.Eq(d => d.Id, id.Value),
            OwnerFilter());

        var document = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
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

                    var updateFilter = Builders<IntentDocument>.Filter.And(
                        byIdAndOwner,
                        Builders<IntentDocument>.Filter.Eq(d => d.CurrentVersion, expectedVersion));
                    var updateResult = await _intents.UpdateOneAsync(
                        session,
                        updateFilter,
                        update,
                        options: null,
                        ct);

                    if (updateResult.ModifiedCount == 0)
                    {
                        var fresh = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
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

            default:
                throw new InvalidOperationException($"Unhandled domain result: {domainResult.GetType().Name}");
        }
    }

    public async Task<IReadOnlyList<Intent>> ListAsync(IReadOnlyList<string>? statuses, CancellationToken ct)
    {
        var session = sessions.Current;
        var filter = statuses is { Count: > 0 }
            ? Builders<IntentDocument>.Filter.And(
                OwnerFilter(),
                Builders<IntentDocument>.Filter.In(d => d.Status, statuses))
            : OwnerFilter();
        // Default sort = sort_key ASC: top of the list is the lexicographically smallest key.
        // Frontend renders this order directly so DnD reorders feed straight back through.
        var documents = session is null
            ? await _intents.Find(filter)
                .SortBy(d => d.SortKey)
                .ToListAsync(ct)
            : await _intents.Find(session, filter)
                .SortBy(d => d.SortKey)
                .ToListAsync(ct);

        var result = new List<Intent>(documents.Count);
        foreach (var doc in documents)
        {
            result.Add(MapToDomain(doc));
        }
        return result;
    }

    public async Task<IntentListPage> ListPagedAsync(IntentListSpec spec, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var session = sessions.Current;
        var fb = Builders<IntentDocument>.Filter;
        var clauses = new List<FilterDefinition<IntentDocument>> { OwnerFilter() };

        if (spec.Statuses is { Count: > 0 })
        {
            clauses.Add(fb.In(d => d.Status, spec.Statuses));
        }

        if (spec.TagId is not null)
        {
            clauses.Add(fb.AnyEq(d => d.TagIds, spec.TagId.Value.Value));
        }

        if (!string.IsNullOrEmpty(spec.Query))
        {
            // Case-insensitive substring match. Uses regex with collection scan; OK for MVP scale.
            var pattern = System.Text.RegularExpressions.Regex.Escape(spec.Query);
            clauses.Add(fb.Regex(d => d.Text, new MongoDB.Bson.BsonRegularExpression(pattern, "i")));
        }

        if (spec.Cursor is not null)
        {
            clauses.Add(BuildCursorFilter(spec.Sort, spec.Cursor));
        }

        var filter = fb.And(clauses);
        var sort = BuildSort(spec.Sort);

        var find = session is null
            ? _intents.Find(filter).Sort(sort)
            : _intents.Find(session, filter).Sort(sort);

        var pageSize = spec.Limit + 1;
        var documents = await find.Limit(pageSize).ToListAsync(ct);

        var hasMore = documents.Count > spec.Limit;
        var pageDocs = hasMore ? documents.Take(spec.Limit).ToList() : documents;

        var items = new List<Intent>(pageDocs.Count);
        foreach (var doc in pageDocs)
        {
            items.Add(MapToDomain(doc));
        }

        IntentListCursor? next = null;
        if (hasMore && pageDocs.Count > 0)
        {
            next = BuildNextCursor(spec.Sort, pageDocs[^1]);
        }

        return new IntentListPage(items, next);
    }

    private static SortDefinition<IntentDocument> BuildSort(IntentListSort sort)
    {
        var sb = Builders<IntentDocument>.Sort;
        return sort switch
        {
            IntentListSort.SortKeyAsc => sb.Combine(sb.Ascending(d => d.SortKey), sb.Ascending(d => d.Id)),
            IntentListSort.UpdatedDesc => sb.Combine(sb.Descending(d => d.UpdatedAt), sb.Ascending(d => d.Id)),
            IntentListSort.CreatedDesc => sb.Combine(sb.Descending(d => d.CreatedAt), sb.Ascending(d => d.Id)),
            IntentListSort.CreatedAsc => sb.Combine(sb.Ascending(d => d.CreatedAt), sb.Ascending(d => d.Id)),
            _ => throw new InvalidOperationException($"Unknown sort: {sort}"),
        };
    }

    private static FilterDefinition<IntentDocument> BuildCursorFilter(IntentListSort sort, IntentListCursor cursor)
    {
        var fb = Builders<IntentDocument>.Filter;
        if (sort == IntentListSort.SortKeyAsc)
        {
            var sortKey = cursor.SortKey ?? string.Empty;
            return fb.Or(
                fb.Gt(d => d.SortKey, sortKey),
                fb.And(fb.Eq(d => d.SortKey, sortKey), fb.Gt(d => d.Id, cursor.Id)));
        }
        var sortValue = cursor.SortValue.UtcDateTime;
        return sort switch
        {
            IntentListSort.UpdatedDesc => fb.Or(
                fb.Lt(d => d.UpdatedAt, sortValue),
                fb.And(fb.Eq(d => d.UpdatedAt, sortValue), fb.Gt(d => d.Id, cursor.Id))),
            IntentListSort.CreatedDesc => fb.Or(
                fb.Lt(d => d.CreatedAt, sortValue),
                fb.And(fb.Eq(d => d.CreatedAt, sortValue), fb.Gt(d => d.Id, cursor.Id))),
            IntentListSort.CreatedAsc => fb.Or(
                fb.Gt(d => d.CreatedAt, sortValue),
                fb.And(fb.Eq(d => d.CreatedAt, sortValue), fb.Gt(d => d.Id, cursor.Id))),
            _ => throw new InvalidOperationException($"Unknown sort: {sort}"),
        };
    }

    private static IntentListCursor BuildNextCursor(IntentListSort sort, IntentDocument doc) => sort switch
    {
        IntentListSort.SortKeyAsc => new IntentListCursor(DateTimeOffset.MinValue, doc.Id, doc.SortKey),
        IntentListSort.UpdatedDesc => new IntentListCursor(
            new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)), doc.Id),
        IntentListSort.CreatedDesc => new IntentListCursor(
            new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)), doc.Id),
        IntentListSort.CreatedAsc => new IntentListCursor(
            new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)), doc.Id),
        _ => throw new InvalidOperationException($"Unknown sort: {sort}"),
    };

    public async Task<DeleteIntentOutcome> DeleteAsync(IntentId id, CancellationToken ct)
    {
        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentRepository.DeleteAsync must run inside IUnitOfWork.ExecuteAsync.");

        var byIdAndOwner = Builders<IntentDocument>.Filter.And(
            Builders<IntentDocument>.Filter.Eq(d => d.Id, id.Value),
            OwnerFilter());

        var deleteIntent = await _intents.DeleteOneAsync(session, byIdAndOwner, options: null, ct);
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

        var byIdAndOwner = Builders<IntentDocument>.Filter.And(
            Builders<IntentDocument>.Filter.Eq(d => d.Id, id.Value),
            OwnerFilter());

        var document = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
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
            Builders<IntentDocument>.Filter.And(
                BuildStatusUpdateFilter(id.Value, originalVersion, originalStatus),
                OwnerFilter()),
            update,
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

        var byIdAndOwner = Builders<IntentDocument>.Filter.And(
            Builders<IntentDocument>.Filter.Eq(d => d.Id, id.Value),
            OwnerFilter());

        var document = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
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

        var updateFilter = Builders<IntentDocument>.Filter.And(
            byIdAndOwner,
            Builders<IntentDocument>.Filter.Eq(d => d.CurrentVersion, expectedVersion));
        var updateResult = await _intents.UpdateOneAsync(
            session,
            updateFilter,
            update,
            options: null,
            ct);

        if (updateResult.ModifiedCount == 0)
        {
            var fresh = await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);
            return fresh is null
                ? new SetIntentTagsOutcome.NotFound()
                : new SetIntentTagsOutcome.VersionConflict(fresh.CurrentVersion);
        }

        return new SetIntentTagsOutcome.Updated(intent, Changed: true);
    }

    public async Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var byIdAndOwner = Builders<IntentDocument>.Filter.And(
            Builders<IntentDocument>.Filter.Eq(d => d.Id, id.Value),
            OwnerFilter());
        var document = session is null
            ? await _intents.Find(byIdAndOwner).FirstOrDefaultAsync(ct)
            : await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);

        return document is null ? null : MapToDomain(document);
    }

    private static IntentDocument MapIntent(Intent intent) => new()
    {
        Id = intent.Id.Value,
        OwnerUserId = intent.OwnerUserId,
        Text = intent.Text,
        Status = intent.Status,
        CurrentVersion = intent.CurrentVersion,
        TagIds = intent.TagIds.Select(t => t.Value).ToList(),
        SortKey = intent.SortKey,
        CreatedAt = intent.CreatedAt.UtcDateTime,
        UpdatedAt = intent.UpdatedAt.UtcDateTime,
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
        ownerUserId: string.IsNullOrWhiteSpace(doc.OwnerUserId) ? CurrentUserIds.LocalDev : doc.OwnerUserId,
        text: doc.Text,
        status: string.IsNullOrWhiteSpace(doc.Status) ? IntentStatusNames.Draft : doc.Status,
        currentVersion: doc.CurrentVersion,
        tagIds: doc.TagIds.Select(v => new TagId(v)).ToList(),
        sortKey: doc.SortKey,
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

using MongoDB.Driver;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Intents;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentRepository : IIntentRepository, IIntentOrderingRepository, ISystemIntentStatusWriter
{
    private readonly MongoIntentReader _reader;
    private readonly MongoIntentContextReader _contextReader;
    private readonly MongoIntentLifecycle _lifecycle;
    private readonly MongoIntentTextEditor _text;
    private readonly MongoIntentStatusMutator _status;
    private readonly MongoIntentOrderingMutator _ordering;

    public MongoIntentRepository(
        IMongoDatabase database,
        MongoSessionAccessor sessions,
        IIntentEventRepository intentEvents)
    {
        _reader = new MongoIntentReader(database, sessions);
        _contextReader = new MongoIntentContextReader(database, sessions);
        _lifecycle = new MongoIntentLifecycle(database, sessions, intentEvents);
        _text = new MongoIntentTextEditor(database, sessions, intentEvents);
        _status = new MongoIntentStatusMutator(database, sessions, intentEvents);
        _ordering = new MongoIntentOrderingMutator(database, sessions);
    }

    public Task<CreateIntentOutcome> CreateAsync(
        Intent intent,
        TextVersion initialVersion,
        IntentStatusChange initialStatusChange,
        IReadOnlyList<Tag> upsertedTags,
        CancellationToken ct) =>
        _lifecycle.CreateAsync(intent, initialVersion, initialStatusChange, upsertedTags, ct);

    public Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct) =>
        _reader.GetByIdAsync(id, ct);

    public Task<ReplaceIntentTextOutcome> ReplaceTextAsync(
        IntentId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct) =>
        _text.ReplaceTextAsync(id, expectedVersion, oldText, newText, changedBy, now, ct);

    public Task<InsertIntentTextAfterLineOutcome> InsertTextAfterLineAsync(
        IntentId id,
        int expectedVersion,
        int afterLine,
        string insertText,
        DateTimeOffset now,
        CancellationToken ct) =>
        _text.InsertTextAfterLineAsync(id, expectedVersion, afterLine, insertText, now, ct);

    public Task<IReadOnlyList<Intent>> ListAsync(IReadOnlyList<string>? statuses, CancellationToken ct) =>
        _reader.ListAsync(statuses, ct);

    public Task<IntentListPage> ListPagedAsync(IntentListSpec spec, CancellationToken ct) =>
        _reader.ListPagedAsync(spec, ct);

    public Task<IntentContextCounts> GetContextCountsAsync(
        IReadOnlyList<string> runningTerminalIds, CancellationToken ct) =>
        _contextReader.GetContextCountsAsync(runningTerminalIds, ct);

    public Task<DeleteIntentOutcome> DeleteAsync(IntentId id, CancellationToken ct) =>
        _lifecycle.DeleteAsync(id, ct);

    public Task<SetIntentStatusOutcome> SetStatusAsync(
        IntentId id,
        string status,
        string? appendText,
        string? reason,
        IntentTrainingAuthor changedBy,
        string source,
        DateTimeOffset now,
        CancellationToken ct) =>
        _status.SetStatusAsync(id, status, appendText, reason, changedBy, source, now, ct);

    public Task<SetIntentTagsOutcome> SetTagsAsync(
        IntentId id,
        int expectedVersion,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset now,
        CancellationToken ct) =>
        _status.SetTagsAsync(id, expectedVersion, tagIds, now, ct);

    public Task SetCleanupLocalStateOnDoneAsync(
        IntentId id,
        bool value,
        DateTimeOffset now,
        CancellationToken ct) =>
        _status.SetCleanupLocalStateOnDoneAsync(id, value, now, ct);

    // System hooks (PR-merge auto-close) reach the intent surface via
    // ISystemIntentStatusWriter — explicit impl keeps it off the public face.
    Task<Intent?> ISystemIntentStatusWriter.GetByIdForSystemAsync(IntentId id, CancellationToken ct) =>
        _reader.GetByIdForSystemAsync(id, ct);

    Task<SetIntentStatusOutcome> ISystemIntentStatusWriter.SetStatusBySystemAsync(
        IntentId id,
        string status,
        string? reason,
        string source,
        DateTimeOffset now,
        CancellationToken ct) =>
        _status.SetStatusBySystemAsync(id, status, reason, source, now, ct);

    // Ordering surface is consumed only via IIntentOrderingRepository (split out to keep the
    // CRUD type under the per-type budget) — implement explicitly so it stays off the public face.
    Task<string?> IIntentOrderingRepository.GetMinSortKeyAsync(CancellationToken ct) =>
        _reader.GetMinSortKeyAsync(ct);

    Task<MoveIntentOutcome> IIntentOrderingRepository.MoveBetweenAsync(
        IntentId id,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct) =>
        _ordering.MoveBetweenAsync(id, beforeId, afterId, ct);
}

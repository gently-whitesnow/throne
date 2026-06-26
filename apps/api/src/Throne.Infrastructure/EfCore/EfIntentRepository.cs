using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.Intents;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Composite EF Core repository that fronts <see cref="IIntentRepository"/>,
/// <see cref="IIntentOrderingRepository"/> and <see cref="ISystemIntentStatusWriter"/>.
/// All actual work is delegated to per-concern services living in <c>Intents/</c> so each
/// file stays well under the per-type budget and mirrors the Mongo decomposition.
/// </summary>
internal sealed class EfIntentRepository(
    EfIntentReader reader,
    EfIntentLifecycle lifecycle,
    EfIntentTextEditor textEditor,
    EfIntentStatusMutator statusMutator,
    EfIntentOrderingMutator orderingMutator,
    EfIntentContextReader contextReader)
    : IIntentRepository, IIntentOrderingRepository, ISystemIntentStatusWriter
{
    public Task<CreateIntentOutcome> CreateAsync(
        Intent intent,
        TextVersion initialVersion,
        IntentStatusChange initialStatusChange,
        IReadOnlyList<Tag> upsertedTags,
        CancellationToken ct) =>
        lifecycle.CreateAsync(intent, initialVersion, initialStatusChange, upsertedTags, ct);

    public Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct) =>
        reader.GetByIdAsync(id, ct);

    public Task<Intent?> GetByIdForSystemAsync(IntentId id, CancellationToken ct) =>
        reader.GetByIdForSystemAsync(id, ct);

    public Task<ReplaceIntentTextOutcome> ReplaceTextAsync(
        IntentId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct) =>
        textEditor.ReplaceTextAsync(id, expectedVersion, oldText, newText, changedBy, now, ct);

    public Task<InsertIntentTextAfterLineOutcome> InsertTextAfterLineAsync(
        IntentId id,
        int expectedVersion,
        int afterLine,
        string insertText,
        DateTimeOffset now,
        CancellationToken ct) =>
        textEditor.InsertTextAfterLineAsync(id, expectedVersion, afterLine, insertText, now, ct);

    public Task<IReadOnlyList<Intent>> ListAsync(IReadOnlyList<string>? statuses, CancellationToken ct) =>
        reader.ListAsync(statuses, ct);

    public Task<IntentListPage> ListPagedAsync(IntentListSpec spec, CancellationToken ct) =>
        reader.ListPagedAsync(spec, ct);

    public Task<IntentContextCounts> GetContextCountsAsync(
        IReadOnlyList<string> runningTerminalIds,
        CancellationToken ct) =>
        contextReader.GetContextCountsAsync(runningTerminalIds, ct);

    public Task<DeleteIntentOutcome> DeleteAsync(IntentId id, CancellationToken ct) =>
        lifecycle.DeleteAsync(id, ct);

    public Task<SetIntentStatusOutcome> SetStatusAsync(
        IntentId id,
        string status,
        string? appendText,
        string? reason,
        IntentTrainingAuthor changedBy,
        string source,
        DateTimeOffset now,
        CancellationToken ct) =>
        statusMutator.SetStatusAsync(id, status, appendText, reason, changedBy, source, now, ct);

    public Task<SetIntentStatusOutcome> SetStatusBySystemAsync(
        IntentId id,
        string status,
        string? reason,
        string source,
        DateTimeOffset now,
        CancellationToken ct) =>
        statusMutator.SetStatusBySystemAsync(id, status, reason, source, now, ct);

    public Task<SetIntentTagsOutcome> SetTagsAsync(
        IntentId id,
        int expectedVersion,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset now,
        CancellationToken ct) =>
        statusMutator.SetTagsAsync(id, expectedVersion, tagIds, now, ct);

    public Task SetCleanupLocalStateOnDoneAsync(
        IntentId id,
        bool value,
        DateTimeOffset now,
        CancellationToken ct) =>
        statusMutator.SetCleanupLocalStateOnDoneAsync(id, value, now, ct);

    public Task<string?> GetMinSortKeyAsync(CancellationToken ct) =>
        reader.GetMinSortKeyAsync(ct);

    public Task<MoveIntentOutcome> MoveBetweenAsync(
        IntentId id,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct) =>
        orderingMutator.MoveBetweenAsync(id, beforeId, afterId, ct);
}

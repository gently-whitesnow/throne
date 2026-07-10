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
/// file stays well under the per-type budget.
/// The bulk of the surface is implemented via EXPLICIT interface members — three ports
/// crowd the same class and explicit impls keep the type's public-member count under the
/// maintainability budget without faking artificial sub-interfaces.
/// </summary>
internal sealed class EfIntentRepository(
    EfIntentReader reader,
    EfIntentLifecycle lifecycle,
    EfIntentTextEditor textEditor,
    EfIntentStatusMutator statusMutator,
    EfIntentTagsMutator tagsMutator,
    EfIntentOrderingMutator orderingMutator,
    EfIntentContextReader contextReader)
    : IIntentRepository, IIntentOrderingRepository, ISystemIntentStatusWriter
{
    Task<CreateIntentOutcome> IIntentRepository.CreateAsync(
        Intent intent,
        TextVersion initialVersion,
        IntentStatusChange initialStatusChange,
        IReadOnlyList<Tag> upsertedTags,
        CancellationToken ct) =>
        lifecycle.CreateAsync(intent, initialVersion, initialStatusChange, upsertedTags, ct);

    Task<Intent?> IIntentRepository.GetByIdAsync(IntentId id, CancellationToken ct) =>
        reader.GetByIdAsync(id, ct);

    Task<Intent?> ISystemIntentStatusWriter.GetByIdForSystemAsync(IntentId id, CancellationToken ct) =>
        reader.GetByIdForSystemAsync(id, ct);

    Task<ReplaceIntentTextOutcome> IIntentRepository.ReplaceTextAsync(
        IntentId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct) =>
        textEditor.ReplaceTextAsync(id, expectedVersion, oldText, newText, changedBy, now, ct);

    Task<InsertIntentTextAfterLineOutcome> IIntentRepository.InsertTextAfterLineAsync(
        IntentId id,
        int expectedVersion,
        int afterLine,
        string insertText,
        DateTimeOffset now,
        CancellationToken ct) =>
        textEditor.InsertTextAfterLineAsync(id, expectedVersion, afterLine, insertText, now, ct);

    Task<IReadOnlyList<Intent>> IIntentRepository.ListAsync(IReadOnlyList<string>? statuses, CancellationToken ct) =>
        reader.ListAsync(statuses, ct);

    Task<IntentListPage> IIntentRepository.ListPagedAsync(IntentListSpec spec, CancellationToken ct) =>
        reader.ListPagedAsync(spec, ct);

    Task<IntentContextCounts> IIntentRepository.GetContextCountsAsync(
        IReadOnlyList<string> runningTerminalIds,
        CancellationToken ct) =>
        contextReader.GetContextCountsAsync(runningTerminalIds, ct);

    Task<DeleteIntentOutcome> IIntentRepository.DeleteAsync(IntentId id, CancellationToken ct) =>
        lifecycle.DeleteAsync(id, ct);

    Task<SetIntentStatusOutcome> IIntentRepository.SetStatusAsync(
        IntentId id,
        string status,
        string? appendText,
        string? reason,
        IntentTrainingAuthor changedBy,
        string source,
        DateTimeOffset now,
        CancellationToken ct) =>
        statusMutator.SetStatusAsync(id, status, appendText, reason, changedBy, source, now, ct);

    Task<SetIntentStatusOutcome> ISystemIntentStatusWriter.SetStatusBySystemAsync(
        IntentId id,
        string status,
        string? reason,
        string source,
        DateTimeOffset now,
        CancellationToken ct) =>
        statusMutator.SetStatusBySystemAsync(id, status, reason, source, now, ct);

    Task<SetIntentTagsOutcome> IIntentRepository.SetTagsAsync(
        IntentId id,
        int expectedVersion,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset now,
        CancellationToken ct) =>
        tagsMutator.SetTagsAsync(id, expectedVersion, tagIds, now, ct);

    Task IIntentRepository.SetCleanupLocalStateOnDoneAsync(
        IntentId id,
        bool value,
        DateTimeOffset now,
        CancellationToken ct) =>
        tagsMutator.SetCleanupLocalStateOnDoneAsync(id, value, now, ct);

    Task<string?> IIntentOrderingRepository.GetMinSortKeyAsync(CancellationToken ct) =>
        reader.GetMinSortKeyAsync(ct);

    Task<MoveIntentOutcome> IIntentOrderingRepository.MoveBetweenAsync(
        IntentId id,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct) =>
        orderingMutator.MoveBetweenAsync(id, beforeId, afterId, ct);
}

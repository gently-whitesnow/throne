using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence boundary for <see cref="IntentCardAttachment"/> (ADR-0052). Reads run on the ambient
/// session (or a transient context); writes (<see cref="UpsertAsync"/> / <see cref="DeleteAsync"/>) must
/// run inside <c>IUnitOfWork.ExecuteAsync</c>. No typed outcomes — attach/detach carry no domain events
/// in this phase (realtime is deferred, ADR-0052).
/// </summary>
public interface IIntentCardAttachmentStore
{
    /// <summary>All attachments on an intent, ordered by <c>created_at</c> ASC.</summary>
    Task<IReadOnlyList<IntentCardAttachment>> ListByIntentAsync(IntentId intentId, CancellationToken ct);

    /// <summary>The attachment with this id, or null when none exists.</summary>
    Task<IntentCardAttachment?> GetAsync(CardAttachmentId id, CancellationToken ct);

    /// <summary>
    /// The attachment for <paramref name="coordinate"/> within an intent, or null — backs the idempotent
    /// re-attach (existing → refresh snapshot; absent → create).
    /// </summary>
    Task<IntentCardAttachment?> GetByCoordinateAsync(
        IntentId intentId, CardCoordinate coordinate, CancellationToken ct);

    /// <summary>Insert a new attachment or overwrite the snapshot/availability of an existing one (by id).</summary>
    Task UpsertAsync(IntentCardAttachment attachment, CancellationToken ct);

    /// <summary>Delete the attachment by id. Returns <see langword="true"/> when a row was removed.</summary>
    Task<bool> DeleteAsync(CardAttachmentId id, CancellationToken ct);
}

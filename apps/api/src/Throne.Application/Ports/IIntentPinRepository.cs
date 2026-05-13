using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Application.Ports;

public interface IIntentPinRepository
{
    /// <summary>
    /// Upsert a pin for (intent, context). When <paramref name="beforeId"/> and
    /// <paramref name="afterId"/> are both null, the pin is appended to the tail
    /// of the context's Pinned list. When either is supplied, the resulting
    /// pin_sort_key is computed via <see cref="FractionalIndex"/>. Re-pinning an
    /// existing pair without pivots is a no-op.
    /// </summary>
    Task<PinIntentOutcome> PinAsync(
        IntentId intentId,
        TagId contextTagId,
        IntentId? beforeId,
        IntentId? afterId,
        DateTimeOffset now,
        CancellationToken ct);

    Task<UnpinIntentOutcome> UnpinAsync(
        IntentId intentId,
        TagId contextTagId,
        CancellationToken ct);

    Task<MovePinOutcome> MoveAsync(
        IntentId intentId,
        TagId contextTagId,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct);

    /// <summary>
    /// Per-context pin entries for each given intent (tag id + pin_sort_key). Caller-scoped;
    /// intents the caller does not own are filtered out. Missing intent ids return an empty list.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<IntentPin>>> GetPinnedInAsync(
        IReadOnlyList<string> intentIds,
        CancellationToken ct);

    /// <summary>
    /// All pins owned by the caller, ordered by (context_tag_id, pin_sort_key ASC).
    /// </summary>
    Task<IReadOnlyList<IntentPin>> ListAllAsync(CancellationToken ct);
}

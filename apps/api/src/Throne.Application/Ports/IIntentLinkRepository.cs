using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;

namespace Throne.Application.Ports;

public enum IntentLinkDirection
{
    Outgoing,
    Incoming,
}

/// <summary>
/// One element of a links projection over an intent. <see cref="Direction"/> is computed
/// relative to the queried intent (Outgoing = stored from_id matches query, Incoming = stored
/// to_id matches query).
/// </summary>
public sealed record IntentLinkView(
    IntentLink Link,
    IntentLinkDirection Direction,
    Intent Other);

public sealed record IntentLinksPage(
    IReadOnlyList<IntentLinkView> Items,
    string? NextCursor);

public interface IIntentLinkRepository
{
    Task<CreateIntentLinkOutcome> CreateAsync(IntentLink link, CancellationToken ct);

    Task<DeleteIntentLinkOutcome> DeleteAsync(
        IntentId fromId,
        IntentId toId,
        string type,
        CancellationToken ct);

    /// <summary>
    /// Reads outgoing + incoming links for the supplied intent. Orphan edges (peer
    /// deleted) are dropped from the projection.
    /// </summary>
    Task<IReadOnlyList<IntentLinkView>> ListByIntentAsync(IntentId intentId, CancellationToken ct);

    Task<IntentLinksPage> ListPagedAsync(
        IntentId intentId,
        IntentLinkDirection? direction,
        string? type,
        int limit,
        string? cursor,
        CancellationToken ct);

    /// <summary>
    /// For each input intent id, returns the count of incoming edges of the supplied
    /// <paramref name="type"/>. Drives the «blocked by N» badge on the board (ADR-0019).
    /// Missing ids in the result map to 0.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> CountIncomingByTypeAsync(
        IReadOnlyList<IntentId> intentIds,
        string type,
        CancellationToken ct);

    /// <summary>
    /// Batch projection of incident edges for many intents. Returns a map from intent id
    /// to its <see cref="IntentLinkView"/> list (direction is computed relative to that
    /// intent). Drives the board's links-summary endpoint without N round-trips. Ids
    /// without incident edges are absent from the map.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<IntentLinkView>>> ListByIntentsAsync(
        IReadOnlyList<IntentId> intentIds,
        CancellationToken ct);
}

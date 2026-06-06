using Throne.Domain.Intents;

namespace Throne.Application.Ports;

/// <summary>
/// Fractional-index ordering surface for the Intent aggregate. Separate from
/// <see cref="IIntentRepository"/> so handlers that don't reorder don't take a
/// dependency on the ordering API.
/// </summary>
public interface IIntentOrderingRepository
{
    /// <summary>
    /// Lowest <c>sort_key</c> currently held by the current user's intents, or null when
    /// the user has none. Used by create-flows to compute a key strictly above the top.
    /// </summary>
    Task<string?> GetMinSortKeyAsync(CancellationToken ct);

    /// <summary>
    /// Reorder an intent so that it sits between the supplied pivots (each by id, optional).
    /// The repository looks up the pivots' sort keys and computes the midpoint via
    /// <see cref="FractionalIndex"/> — clients never send keys.
    /// At least one pivot must be supplied.
    /// </summary>
    Task<MoveIntentOutcome> MoveBetweenAsync(
        IntentId id,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct);
}

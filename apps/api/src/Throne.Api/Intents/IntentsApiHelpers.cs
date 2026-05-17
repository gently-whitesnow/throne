using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Api.Intents;

/// <summary>
/// Shared per-request helpers for the four Intents controllers
/// (IntentsController, IntentPinsController, IntentLinksController,
/// IntentAttachmentsController). Concentrates tag / pin lookups in a single
/// Singleton — endpoints resolve this via the DI container and call the
/// public methods only; the underlying repositories stay encapsulated.
/// </summary>
public sealed class IntentsApiHelpers(IntentsApiTagMap tags, IntentsApiPinLookup pins)
{
    public Task<IReadOnlyDictionary<string, Tag>> BuildTagMapAsync(
        IEnumerable<TagId> tagIds,
        CancellationToken ct) =>
        tags.BuildAsync(tagIds, ct);

    public Task<IReadOnlyList<IntentPin>> GetPinnedInAsync(string intentId, CancellationToken ct) =>
        pins.GetSingleAsync(intentId, ct);

    public Task<IReadOnlyDictionary<string, IReadOnlyList<IntentPin>>> GetPinnedInAsync(
        IReadOnlyList<string> intentIds,
        CancellationToken ct) =>
        pins.GetManyAsync(intentIds, ct);
}

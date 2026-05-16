using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Api.Intents;

/// <summary>
/// Shared per-request helpers for the four Intents controllers
/// (IntentsController, IntentPinsController, IntentLinksController,
/// IntentAttachmentsController). Concentrates tag / pin lookups in a single
/// Singleton — the controllers and the per-endpoint static helpers resolve
/// this via the DI container.
/// </summary>
public sealed class IntentsApiHelpers(ITagRepository tags, IIntentPinRepository pinRepository)
{
    public ITagRepository Tags => tags;
    public IIntentPinRepository PinRepository => pinRepository;

    public Task<IReadOnlyDictionary<string, Tag>> BuildTagMapAsync(
        IEnumerable<TagId> tagIds,
        CancellationToken ct) =>
        IntentsApiTagMap.BuildAsync(tags, tagIds, ct);

    public Task<IReadOnlyList<IntentPin>> GetPinnedInAsync(string intentId, CancellationToken ct) =>
        IntentsApiPinLookup.GetSingleAsync(pinRepository, intentId, ct);

    public Task<IReadOnlyDictionary<string, IReadOnlyList<IntentPin>>> GetPinnedInAsync(
        IReadOnlyList<string> intentIds,
        CancellationToken ct) =>
        pinRepository.GetPinnedInAsync(intentIds, ct);
}

using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Api.Intents;

public sealed class IntentsApiPinLookup(IIntentPinRepository pinRepository)
{
    public async Task<IReadOnlyList<IntentPin>> GetSingleAsync(string intentId, CancellationToken ct)
    {
        var map = await pinRepository.GetPinnedInAsync([intentId], ct);
        return map.TryGetValue(intentId, out var pins) ? pins : Array.Empty<IntentPin>();
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyList<IntentPin>>> GetManyAsync(
        IReadOnlyList<string> intentIds,
        CancellationToken ct) =>
        pinRepository.GetPinnedInAsync(intentIds, ct);
}

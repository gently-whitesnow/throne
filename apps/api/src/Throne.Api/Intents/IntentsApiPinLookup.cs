using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Api.Intents;

internal static class IntentsApiPinLookup
{
    public static async Task<IReadOnlyList<IntentPin>> GetSingleAsync(
        IIntentPinRepository pinRepository,
        string intentId,
        CancellationToken ct)
    {
        var map = await pinRepository.GetPinnedInAsync([intentId], ct);
        return map.TryGetValue(intentId, out var pins) ? pins : Array.Empty<IntentPin>();
    }
}
